# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A real-time motion capture and gesture recognition system for the KAIST GCT555 (3D Interaction) course. A Python backend streams pose/hand/face landmark data to a Unity client over TCP sockets, and serves annotated video over HTTP.

## Setup

```
pip install -r requirements.txt
download_model.bat          # Windows — downloads MediaPipe .task models to models/
```

## Running Servers

Each server is independent and runs on its own ports:

| Server | Script | Socket Port | Web Port |
|--------|--------|-------------|----------|
| Pose detection | `python server_pose.py` | 5050 | 5000 |
| Hand landmarks | `python server_hand.py` | 5051 | 5001 |
| Gesture recognition | `python server_gesture.py` | 5051 | 5001 |
| Face landmarks | `python server_face.py` | 5052 | 5002 |

Web endpoints available on each server: `/` (index), `/video_feed` (MJPEG), `/snapshot` (JPEG).

## Architecture

### Data Flow

```
Camera (OpenCV)
  → MediaPipe inference
  → depth_module: smooth + scale depth, build JSON payloads
  → Socket thread → Unity StreamClient → 3D landmark spheres
  → Flask thread  → /video_feed (browser) + /snapshot (QuadDisplay texture)
```

### Python Backend

**`depth_module.py`** is the core transformation layer. All servers import from it:
- `DepthConfig` — per-server tunable parameters (alpha smoothing, scale, clamp, inversion)
- `DepthState` — holds frame-to-frame exponential moving average state
- `build_pose_payload()`, `build_hand_payloads()`, `build_face_payloads()` — produce the JSON sent to Unity

**Depth modes** (set in DepthConfig):
- `face_transform_plus_local` — global Z from face 4×4 transform matrix + per-landmark local offset
- `pose_face_abs` — pose with absolute depth from face + relative offset
- `pose_world` / `hand_world` — fallback using world landmarks only

**`gesture_module.py`** — heuristic custom gesture detection (Gun, MiddleFinger, Rock) layered on top of MediaPipe's built-in gesture recognizer (Closed_Fist, Open_Palm, etc.).

Each `server_*.py` runs three threads concurrently: camera capture, socket server, Flask server. `server_pose.py` additionally spawns a face detection thread for absolute depth calibration.

### Unity Client (in `../GCT555_Client/`)

- **`StreamClient.cs`** — TcpClient on a background thread; parses JSON into `PoseData`/`HandData`/`FaceData`; maps normalized (0–1) XY coordinates onto a quad surface; calculates pseudo-depth from bounding box size (`UpdateHybridVisuals`)
- **`StreamManager.cs`** — orchestrates multiple `StreamClient` instances for a multi-wall setup (`WallConfig` with IP, ports, quad transform)
- **`LandmarkData.cs`** — JSON deserialization schema (`Landmark`, `PoseData`, `HandData`, `FaceData`, `Hand`)
- **`QuadDisplay.cs`** — coroutine that fetches `/snapshot` at ~30 FPS and applies it as a quad texture; destroys old textures each frame to prevent leaks

### JSON Payload Structure

```json
{
  "landmarks": [{"x": 0.5, "y": 0.3, "z": -0.1, "visibility": 0.9, "worldPosition": [x, y, z]}],
  "world_landmarks": [...],
  "handedness": "Right",
  "gesture": "Open_Palm"
}
```

## Key Design Notes

- **Smoothing:** Exponential moving average with configurable alpha (default 0.2–0.35) in `DepthState`. Lower alpha = smoother but more lag.
- **Hybrid depth for pose:** Absolute Z from face transformation matrix combined with relative world-landmark Z offset, giving stable depth without drift.
- **Custom gestures** are evaluated after MediaPipe's own gesture recognizer; if no built-in gesture matches, `detect_custom_gesture()` applies finger-distance heuristics.
- Unity `StreamClient` has a `mirrorX` toggle to flip landmark X for webcam-feel display.
