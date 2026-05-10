def detect_custom_gesture(landmarks, handedness=None):
    def get_dist(lm1, lm2):
        return ((lm1.x - lm2.x)**2 + (lm1.y - lm2.y)**2)**0.5

    def get_dist_3d(lm1, lm2):
        return ((lm1.x - lm2.x)**2 + (lm1.y - lm2.y)**2 + (lm1.z - lm2.z)**2)**0.5

    # 1. Check if the four fingers are open (distance to wrist)
    # This works regardless of hand rotation (e.g., pointing horizontally)
    index_is_open = get_dist(landmarks[8], landmarks[0]) > get_dist(landmarks[6], landmarks[0])
    middle_is_open = get_dist(landmarks[12], landmarks[0]) > get_dist(landmarks[10], landmarks[0])
    ring_is_open = get_dist(landmarks[16], landmarks[0]) > get_dist(landmarks[14], landmarks[0])
    pinky_is_open = get_dist(landmarks[20], landmarks[0]) > get_dist(landmarks[18], landmarks[0])

    # 2. Check if the thumb is open using distance
    thumb_is_open = get_dist(landmarks[4], landmarks[17]) > get_dist(landmarks[2], landmarks[17])

    # 3. Pinch detection: normalize thumb-to-fingertip distance by hand scale (wrist → middle MCP)
    hand_scale = get_dist(landmarks[0], landmarks[9])
    pinch_threshold = hand_scale * 0.7
    thumb_index_pinch = get_dist(landmarks[4], landmarks[8]) < pinch_threshold
    thumb_middle_pinch = get_dist(landmarks[4], landmarks[12]) < pinch_threshold

    if handedness == "Left":
        # 4. Left: thumb + index pinching, middle/ring/pinky extended
        if thumb_index_pinch and middle_is_open and ring_is_open and pinky_is_open:
            return "Left_Pinch_Second"

        # 5. Left: thumb + middle pinching, index/ring/pinky extended
        if thumb_middle_pinch and index_is_open and ring_is_open and pinky_is_open:
            return "Left_Pinch_Middle"

    elif handedness == "Right":
        # 6. Right: thumb and index tips touching
        if get_dist_3d(landmarks[4], landmarks[8]) < pinch_threshold/4:
            return "Right_Grab"

        # 7. Right: all fingers extended
        if thumb_is_open and index_is_open and middle_is_open and ring_is_open and pinky_is_open:
            return "Right_Release"
        # Hand-agnostic gestures
    # if thumb_is_open and index_is_open and not middle_is_open and not ring_is_open and not pinky_is_open:
    #     return "Custom_Gun"

    # if not thumb_is_open and not index_is_open and middle_is_open and not ring_is_open and not pinky_is_open:
    #     return "Custom_MiddleFinger"

    # if index_is_open and pinky_is_open and not middle_is_open and not ring_is_open and not thumb_is_open:
    #     return "Custom_Rock"
    return None
