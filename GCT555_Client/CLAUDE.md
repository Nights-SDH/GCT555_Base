# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

GCT555 is a real-time motion capture and interactive 3D visualization system. A Python server uses MediaPipe to track pose/hand/face landmarks from a webcam, streaming the data over TCP to a Unity client that drives a Humanoid avatar and interactive objects.

## Running the Project

**Server (Python, in `GCT555_Server/`):**
```bash
conda create -n gct555 python=3.10
conda activate gct555
pip install -r requirements.txt
download_model.bat        # Windows: download MediaPipe models
python server_pose.py     # starts pose tracking on port 5050
```

**Client (Unity):**
- Unity 6000.3.4f1 (URP 17.3.0)
- Open project via Unity Hub → select `GCT555_Client` folder
- Press Play in the Editor to run; server must be running first

## Architecture

All runtime scripts live in [Assets/Scripts/](Assets/Scripts/).

### Data Flow

1. Server sends newline-delimited JSON over TCP (ports 5050/5051/5052 for pose/hand/face)
2. `StreamClient` receives in a background thread, parses JSON on the main thread each frame
3. Parsed landmarks are materialized as GameObjects in world space and stored in `activeLandmarks`
4. `MixamoRetargeter` reads `StreamClient.latestPoseData` → computes bone rotations → drives Humanoid avatar
5. `GestureEffect` reads `StreamClient.currentGesture` → changes object color/position/audio
6. `QuadDisplay` polls an HTTP snapshot endpoint and updates a `Renderer` texture at ~30 FPS

### Key Scripts

| Script | Role |
|--------|------|
| [StreamClient.cs](Assets/Scripts/StreamClient.cs) | TCP client; receives, parses, and visualizes landmarks |
| [StreamManager.cs](Assets/Scripts/StreamManager.cs) | Configures two wall setups; creates `StreamClient` instances |
| [MixamoRetargeter.cs](Assets/Scripts/MixamoRetargeter.cs) | Maps MediaPipe landmarks → Humanoid bone rotations |
| [AnimationManager.cs](Assets/Scripts/AnimationManager.cs) | Sets Animator parameters (`IsClose`, `IsDance`) from pose data |
| [GestureEffect.cs](Assets/Scripts/GestureEffect.cs) | Reacts to hand gesture strings with color/position/sound |
| [QuadDisplay.cs](Assets/Scripts/QuadDisplay.cs) | Fetches HTTP video frames and applies them as textures |
| [LandmarkData.cs](Assets/Scripts/LandmarkData.cs) | JSON-serializable data structures (`PoseData`, `Hand`, `Face`, `DepthInfo`) |

### Coordinate System & Depth

- Server landmark XY is normalized (0–1); `StreamClient` maps them onto a quad surface in Unity world space
- `mirrorX = true` by default for webcam mirror behavior
- Depth resolution priority: `DepthInfo.per_landmark_z` → `world_landmarks.z` → `normalized.z`
- Pseudo-depth fallback: bounding-box size heuristic (smaller box → farther)
- XY positions are scaled inversely with depth to prevent apparent landmark spread changes at different distances

### Coordinate Conversion (MediaPipe → Unity)

MediaPipe: X=right, Y=down, Z=toward camera  
Unity: X=right, Y=up, Z=forward  
`MixamoRetargeter` applies this flip when computing bone direction vectors.

### Initialization Timing

`MixamoRetargeter` waits 3 seconds at startup before initializing to allow `StreamManager` to finish spawning `StreamClient` instances. Components that need a `StreamClient` reference search for one at `Start`/first `Update` if not pre-assigned in the Inspector.

### JSON Protocol

Each TCP message is one JSON line. Top-level keys depend on client type:
- **Pose**: `{ landmarks, world_landmarks, depth }`
- **Hand**: `{ handedness, gesture, landmarks, world_landmarks, depth }`
- **Face**: `{ landmarks, face_pose, depth }`

`depth` contains `{ mode, global_z, per_landmark_z[] }`.
