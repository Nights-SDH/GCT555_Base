import cv2
import mediapipe as mp
import socket
import threading
import json
import time
import numpy as np
from flask import Flask, Response

from mediapipe.tasks import python
from mediapipe.tasks.python import vision

#---------------------------
from depth_module import DepthConfig, DepthState, build_hand_payloads
from gesture_module import detect_custom_gesture

depth_state = DepthState(
    DepthConfig(
        smoothing_alpha=0.35,
        pose_invert_world_z=False,
        clamp_min=-5.0,
        clamp_max=5.0,
    )
)
#---------------------------

# Configuration
SOCKET_HOST = '0.0.0.0'
SOCKET_PORT = 5051
WEB_PORT = 5001
CAMERA_INDEX = 0
DEBUG_MODE = True
MODEL_PATH = 'models/gesture_recognizer.task'

# Global variables to share data between threads
current_frame = None
current_gesture_result = None
lock = threading.Lock()

# Initialize Flask
app = Flask(__name__)

# MediaPipe Hand Connections (Standard)
HAND_CONNECTIONS = [
    (0, 1), (1, 2), (2, 3), (3, 4),           # Thumb
    (0, 5), (5, 6), (6, 7), (7, 8),           # Index
    (0, 9), (9, 10), (10, 11), (11, 12),      # Middle
    (0, 13), (13, 14), (14, 15), (15, 16),    # Ring
    (0, 17), (17, 18), (18, 19), (19, 20)     # Pinky
]

# Gesture label → display string
GESTURE_DISPLAY = {
    "None":         "None",
    "Closed_Fist":  "Fist",
    "Open_Palm":    "Open Hand",
    "Pointing_Up":  "Pointing",
    "Thumb_Down":   "Thumbs Down",
    "Thumb_Up":     "Thumbs Up",
    "Victory":      "Peace / V",
    "ILoveYou":     "I Love You",
}


def draw_landmarks_on_image(rgb_image, detection_result):
    annotated_image = np.copy(rgb_image)
    height, width, _ = annotated_image.shape

    if detection_result is None or not detection_result.hand_landmarks:
        return annotated_image

    for hand_idx, hand_landmarks in enumerate(detection_result.hand_landmarks):
        # Draw connections
        for connection in HAND_CONNECTIONS:
            start_idx = connection[0]
            end_idx = connection[1]

            start_lm = hand_landmarks[start_idx]
            end_lm = hand_landmarks[end_idx]

            start_point = (int(start_lm.x * width), int(start_lm.y * height))
            end_point = (int(end_lm.x * width), int(end_lm.y * height))

            cv2.line(annotated_image, start_point, end_point, (0, 255, 0), 2)

        # Draw landmarks
        for lm in hand_landmarks:
            x = int(lm.x * width)
            y = int(lm.y * height)
            cv2.circle(annotated_image, (x, y), 3, (0, 0, 255), -1)

        # Draw gesture label near wrist
        wrist = hand_landmarks[0]
        wrist_px = (int(wrist.x * width), int(wrist.y * height))

        gesture_label = "unknown"

        custom_gesture = detect_custom_gesture(hand_landmarks)
        if custom_gesture:
            gesture_label = custom_gesture
        else:
            if detection_result.gestures and hand_idx < len(detection_result.gestures):
                gestures = detection_result.gestures[hand_idx]
                if gestures:
                    raw_label = gestures[0].category_name
                    gesture_label = GESTURE_DISPLAY.get(raw_label, raw_label)

        if gesture_label.startswith("Custom_"):
            gesture_label = gesture_label.replace("Custom_", "") + " (Custom)"

        cv2.putText(
            annotated_image,
            gesture_label,
            (wrist_px[0] - 30, wrist_px[1] + 30),
            cv2.FONT_HERSHEY_SIMPLEX,
            1.0,
            (255, 255, 0),
            2,
            cv2.LINE_AA
        )

    return annotated_image


def build_gesture_hand_payloads(gesture_result, depth_state):
    """
    Extends build_hand_payloads output with gesture information.
    """
    if not gesture_result or not gesture_result.hand_landmarks:
        return []

    hand_payloads = build_hand_payloads(gesture_result, depth_state)

    # Attach ML gesture info to each hand payload
    for idx, payload in enumerate(hand_payloads):
        gesture_label = "unknown"
        confidence = 0.0

        custom_gesture = detect_custom_gesture(gesture_result.hand_landmarks[idx])
        if custom_gesture:
            gesture_label = custom_gesture
            confidence = 1.0 
        else:
            if gesture_result.gestures and idx < len(gesture_result.gestures):
                gestures = gesture_result.gestures[idx]
                if gestures:
                    gesture_label = gestures[0].category_name
                    confidence = round(float(gestures[0].score), 3)

        payload["gesture"] = gesture_label
        payload["gesture_confidence"] = confidence

    return hand_payloads


def socket_server_thread():
    """Handles the socket connection to Unity."""
    server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    try:
        server_socket.bind((SOCKET_HOST, SOCKET_PORT))
        server_socket.listen(1)
        print(f"[Socket] Listening on {SOCKET_HOST}:{SOCKET_PORT}")

        while True:
            client_socket, addr = server_socket.accept()
            print(f"[Socket] Connected by {addr}")
            try:
                while True:
                    global current_gesture_result
                    data_to_send = None

                    with lock:
                        if current_gesture_result and current_gesture_result.hand_landmarks:
                            hands_data = build_gesture_hand_payloads(current_gesture_result, depth_state)
                            data_to_send = json.dumps({
                                'hands': hands_data
                            })

                    if data_to_send:
                        client_socket.sendall((data_to_send + "\n").encode('utf-8'))

                    time.sleep(0.033)
            except (ConnectionResetError, BrokenPipeError):
                print(f"[Socket] Disconnected from {addr}")
            finally:
                client_socket.close()

    except Exception as e:
        print(f"[Socket] Server Error: {e}")
    finally:
        server_socket.close()


def generate_frames():
    """Generator function for the Flask video stream."""
    while True:
        with lock:
            if current_frame is None:
                time.sleep(0.01)
                continue
            ret, buffer = cv2.imencode('.jpg', current_frame)
            frame_bytes = buffer.tobytes()

        yield (b'--frame\r\n'
               b'Content-Type: image/jpeg\r\n\r\n' + frame_bytes + b'\r\n')


@app.route('/video_feed')
def video_feed():
    return Response(generate_frames(), mimetype='multipart/x-mixed-replace; boundary=frame')


@app.route('/snapshot')
def snapshot():
    with lock:
        if current_frame is None:
            return "No frame", 503
        ret, buffer = cv2.imencode('.jpg', current_frame)
        return Response(buffer.tobytes(), mimetype='image/jpeg')


@app.route('/')
def index():
    return "<h1>MediaPipe Gesture Server</h1><p><a href='/video_feed'>View Stream</a></p><p>Socket: 5051 | Gestures: Closed_Fist, Open_Palm, Pointing_Up, Thumb_Down, Thumb_Up, Victory, ILoveYou</p>"


def main():
    global current_frame, current_gesture_result

    # Start Socket Server thread
    t_socket = threading.Thread(target=socket_server_thread, daemon=True)
    t_socket.start()

    # Start Flask thread
    t_flask = threading.Thread(
        target=lambda: app.run(host='0.0.0.0', port=WEB_PORT, debug=False, use_reloader=False),
        daemon=True
    )
    t_flask.start()
    print(f"[Web] Server running on http://localhost:{WEB_PORT}")

    # Set up MediaPipe Gesture Recognizer
    base_options = python.BaseOptions(model_asset_path=MODEL_PATH)
    options = vision.GestureRecognizerOptions(
        base_options=base_options,
        num_hands=2
    )
    recognizer = vision.GestureRecognizer.create_from_options(options)

    # Video Capture
    cap = cv2.VideoCapture(CAMERA_INDEX)

    if not cap.isOpened():
        print("Error: Could not open camera.")
        return

    print("Starting Main Loop...")
    while cap.isOpened():
        success, image = cap.read()
        if not success:
            print("Ignoring empty camera frame.")
            continue

        image = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
        mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=image)

        gesture_result = recognizer.recognize(mp_image)

        # Draw landmarks and gesture labels
        annotated_image = draw_landmarks_on_image(image, gesture_result)
        annotated_image_bgr = cv2.cvtColor(annotated_image, cv2.COLOR_RGB2BGR)

        with lock:
            current_gesture_result = gesture_result
            current_frame = annotated_image_bgr

        if DEBUG_MODE:
            cv2.imshow('MediaPipe Gesture - Server', annotated_image_bgr)
            if cv2.waitKey(5) & 0xFF == 27:
                break

    cap.release()
    cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
