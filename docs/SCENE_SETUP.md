# SuperHeli — Scene Setup Guide

This guide explains how to assemble a working gameplay scene from scratch using the scripts in `Assets/Scripts/SimCore/`. Follow it in order.

---

## Prerequisites

| Requirement | Details |
|---|---|
| Unity version | 2021.3 LTS or newer (project uses HDRP) |
| Render Pipeline | High Definition Render Pipeline (HDRP) |
| Input System | Unity Input System package (new, not legacy) — enable in Project Settings → Player → Active Input Handling → **Both** or **Input System Package** |
| Required packages | Unity Input System, HDRP |

---

## Scene Hierarchy Overview

```
Scene
├── PlayerHelicopter          ← helicopter root (has Rigidbody)
│   ├── Body (or root mesh)
│   └── CameraPivot           ← pivot for cockpit/look-down cameras
│       └── FPV Camera        ← cockpit camera
├── Follow Camera             ← third-person follow camera (root level)
├── LookDownCamera            ← top-down camera (root level)
├── MenuCamera                ← static cinematic camera for main menu
├── GameManager               ← invisible manager object
├── SpawnPoint                ← empty Transform, helicopter starts here
├── Helipad                   ← landing pad mesh + collider
├── Wind                      ← WindZone component
├── Level2                    ← parent for Level 2 objects
│   ├── Ring1                 ← CheckpointRing
│   └── Ring2                 ← CheckpointRing
├── NPC (optional)            ← patrol character
├── Terrain
└── Lighting / Sky
```

---

## Component Setup Per GameObject

### PlayerHelicopter

This is the helicopter's root GameObject. It must have a **Rigidbody**.

| Component | Notes |
|---|---|
| `Rigidbody` | Mass: set by SimulatorDriver at runtime. Interpolate: **Interpolate**. |
| `BoxCollider` | Sized to fit the helicopter body. Not a trigger. |
| `SimulatorDriver` | Core input + physics bridge. |
| `HelicopterPlayer` | Drives rotor spin animation. |
| `LandingChecker` | Validates landings. Fires `OnLanding` event. |
| `CrashHandler` | Listens to `LandingChecker.OnLanding`, handles crash/respawn fade. |
| `HeliInput` | Reads gamepad/keyboard input. Added automatically by SimulatorDriver if missing. |
| `SimulatorHUD` | Instrument panel (altitude, speed, heading, gimbal). |
| `EngineAudio` | Adjusts audio pitch/volume based on collective. |
| `WindForce` | Reads WindZone and applies aerodynamic wind force. |
| `MissionManager` | Mission state machine. **Disable this component by default** — MainMenuManager enables it when needed. |
| `MissionHUD` | Mission overlay (task text, timer, hover progress). **Disable by default.** |

#### SimulatorDriver — Inspector fields

| Field | Value |
|---|---|
| Control Entity Id | `player` |
| Rb | Drag in the **Rigidbody** component from this same GameObject |
| Spawn Point | Drag in the **SpawnPoint** GameObject |

#### HelicopterPlayer — Inspector fields

| Field | Value |
|---|---|
| Rotor Transform | Drag in the rotor bone/mesh that should spin |
| Max Rotor RPM | `600` (default) |

#### SimulatorHUD — Inspector fields

| Field | Value |
|---|---|
| Driver | Drag in **SimulatorDriver** from this GameObject |
| Show | `true` |

#### EngineAudio — Inspector fields

| Field | Value |
|---|---|
| Exterior Audio | Drag in the **Exterior AudioSource** |
| Interior Audio | Drag in the **Interior AudioSource** |
| Pitch Min / Max | `0.85` / `1.15` |
| Volume Min / Max | `0.6` / `1.0` |
| Smoothing Time | `0.5` |

#### MissionManager — Inspector fields

| Field | Value |
|---|---|
| Driver | Drag in **SimulatorDriver** |
| Level Number | `1` (default; MainMenuManager overrides at runtime) |
| Level 2 Checkpoints | Size 2 → drag in Ring1, Ring2 |
| Return Scene Name | Leave empty for Level 1/2. Set `"FirstLevel"` for Level 7 scene. |
| Rescue Zone Transform | Level 7 only — empty GameObject at rescue location |
| Base Transform | Level 7 only — empty GameObject at base/helipad |
| Rescue NPCs | Level 7 only — drag in RescueNPC components |

#### WindForce — Inspector fields

| Field | Value |
|---|---|
| Drag Coefficient | `150` (matches SimulatorConfig.linearDrag) |

---

### CameraPivot

Child of PlayerHelicopter. Attach **CameraLook** here.

#### CameraLook — Inspector fields

| Field | Value |
|---|---|
| Tilt Transform | Leave empty |
| **Cameras** (order matters!) | [0] FPV Camera, [1] Follow Camera, [2] LookDownCamera |
| Exterior Audio | Drag in the **Exterior AudioSource** |
| Interior Audio | Drag in the **Interior AudioSource** |

> **Camera order is critical.** Index 0 = FPV (interior audio), Index 1 = Follow (exterior audio), Index 2 = LookDown (interior audio). The audio logic depends on this order.

---

### FPV Camera

Child of CameraPivot. Cockpit perspective.

| Component | Notes |
|---|---|
| `Camera` | FOV ~72°, near clip 0.3 |
| HDRP Additional Camera Data | Add automatically via HDRP |

---

### Follow Camera

Root-level GameObject (not a child of the helicopter).

| Component | Notes |
|---|---|
| `Camera` | FOV 60°, depth -1 |
| `CameraFollow` | Follows the helicopter smoothly |
| `AudioListener` | **Only one AudioListener allowed in scene** — put it here |
| HDRP Additional Camera Data | |

#### CameraFollow — Inspector fields

| Field | Value |
|---|---|
| Target | Drag in **PlayerHelicopter** transform |
| Offset | `(0, 10, -12)` |
| Follow Speed | `6` |
| Look Damp | `6` |

---

### LookDownCamera

Root-level or child of CameraPivot.

| Component | Notes |
|---|---|
| `Camera` | Rotation (90°, 0°, 0°) — points straight down |
| HDRP Additional Camera Data | |

---

### MenuCamera

Root-level. Static cinematic camera used during the main menu.

| Component | Notes |
|---|---|
| `Camera` | Position near the helipad with a nice angle |
| HDRP Additional Camera Data | |

Position suggestion: look toward the helipad from an elevated side angle. The helicopter should be visible in the background while the menu is shown.

---

### Audio Sources

Create two **AudioSource** GameObjects (or place them on any persistent object — GameManager works well):

| Name | Settings |
|---|---|
| **Exterior Audio** | Loop: on. Play On Awake: on. Attach rotor/exterior sound clip. |
| **Interior Audio** | Loop: on. Play On Awake: on. Attach interior/cockpit sound clip. |

Both sources are referenced by both **CameraLook** (for muting) and **EngineAudio** (for pitch/volume). Wire both fields in both components.

---

### GameManager

Empty GameObject at root level. Attach:

| Component | Notes |
|---|---|
| `MainMenuManager` | Main menu controller |
| `PauseManager` | Handles P key / gamepad Start pause |

#### MainMenuManager — Inspector fields

| Field | Value |
|---|---|
| Menu Camera | Drag in **MenuCamera** |
| Camera Look | Drag in **CameraLook** (on CameraPivot) |
| Driver | Drag in **SimulatorDriver** |
| Helicopter Rb | Drag in the **Rigidbody** on PlayerHelicopter |
| Mission Manager | Drag in **MissionManager** |
| Mission HUD | Drag in **MissionHUD** |
| Hud Components | Drag in all **SimulatorHUD** components |

> PauseManager has no Inspector fields — just add it as a component.

---

### SpawnPoint

Empty GameObject. Position it on or just above the helipad where the helicopter should spawn.

> SimulatorDriver looks for a GameObject named exactly `"SpawnPoint"` if the field is not set in Inspector. Naming it correctly is enough.

---

### Wind

Add a **Wind Zone** component (Hierarchy → 3D Object → Wind Zone).

| Setting | Value |
|---|---|
| Mode | Directional |
| Wind Main | Set by MissionManager at runtime (0 = no wind) |

Position doesn't matter for directional wind — only rotation (direction) matters. MissionManager sets rotation at runtime.

---

## Level 2 — Checkpoint Rings

1. Create empty root GameObject: `Level2`
2. Create two children: `Ring1`, `Ring2`
3. Add **CheckpointRing** component to each
4. Position Ring1 ~100m ahead of helipad at target altitude
5. Position Ring2 ~100m behind Ring1 (return route)
6. Assign Ring1 and Ring2 to **MissionManager → Level 2 Checkpoints** array (index 0 and 1)

Both rings start **disabled** — MissionManager activates them one at a time.

#### CheckpointRing — Inspector fields

| Field | Default |
|---|---|
| Radius | `12` |
| Segments | `48` |

---

## NPC Patrol Setup

1. Download character from Mixamo as **T-pose, With Skin** → drag into scene
2. Download animations **Without Skin**: `@Idle`, `@Start`, `@Walk`, `@Stop`
3. Place all FBX files in `Assets/NPC/`
4. For all animation FBX: Rig tab → **Humanoid**, Avatar Definition → **Copy From Other Avatar** → select T-pose character's avatar
5. Create **Animator Controller** (`Assets/NPC/NPC_Controller.controller`):
   - Parameter: `bool isWalking`
   - States: Idle, StartWalk, Walk (loop), Stop
   - Idle → StartWalk: `isWalking = true`, no exit time
   - StartWalk → Walk: has exit time (1.0)
   - Walk → Stop: `isWalking = false`, no exit time
   - Stop → Idle: has exit time (1.0)
6. Add **Animator** to NPC, assign NPC_Controller
7. Add **NPCPatrol** to NPC
8. Create 4–6 empty GameObjects as waypoints around the hangar
9. Assign waypoints to **NPCPatrol → Waypoints** array

#### NPCPatrol — Inspector fields

| Field | Default |
|---|---|
| Walk Speed | `1.4` |
| Turn Speed | `5` |
| Waypoint Tolerance | `0.5` |
| Start Clip Duration | Match your @Start animation length |
| Stop Clip Duration | Match your @Stop animation length |
| Idle Wait Range | `(3, 8)` |

---

## Level 7 (Mountain Rescue) — Additional Setup

Level 7 is a **separate Unity scene** (`Level7`).

The Level 7 scene needs the same base setup as above, plus:

1. **Rescue Zone**: empty GameObject at rescue location → assign to MissionManager → Rescue Zone Transform
2. **Base Transform**: empty GameObject at helipad → assign to MissionManager → Base Transform
3. Two NPCs with **RescueNPC** component → assign to MissionManager → Rescue NPCs
4. **MissionManager**: Level Number = `7`, Return Scene Name = `"FirstLevel"`
5. **WindZone**: MissionManager sets wind automatically (4 m/s, turbulence 0.4)
6. Optional snow: Hierarchy → Effects → Particle System, configure as snow

#### RescueNPC — Inspector fields

| Field | Default |
|---|---|
| Pickup Radius | `15` |
| Passenger Mass Kg | `150` |
| Seat Offset | Position relative to helicopter where NPC sits |
| Is Injured | `false` (true for the injured survivor) |

---

## Build Settings

Add all scenes to **File → Build Settings**:

| Scene | Notes |
|---|---|
| `Assets/FirstLevel.unity` | Main scene — contains the menu and Levels 1 & 2 |
| `Assets/Level7.unity` | Mountain rescue scene |

Ensure FirstLevel is **index 0** in the build list.

---

## Quick Reference: What Goes Where

| Script | GameObject |
|---|---|
| SimulatorDriver | PlayerHelicopter |
| HelicopterPlayer | PlayerHelicopter |
| LandingChecker | PlayerHelicopter |
| CrashHandler | PlayerHelicopter |
| HeliInput | PlayerHelicopter (auto-added) |
| SimulatorHUD | PlayerHelicopter |
| EngineAudio | PlayerHelicopter |
| WindForce | PlayerHelicopter |
| MissionManager | PlayerHelicopter |
| MissionHUD | PlayerHelicopter |
| CameraLook | CameraPivot (child of PlayerHelicopter) |
| CameraFollow | Follow Camera |
| AudioListener | Follow Camera |
| MainMenuManager | GameManager |
| PauseManager | GameManager |
| CheckpointRing | Ring1 / Ring2 (children of Level2) |
| NPCPatrol | NPC root |
| RescueNPC | NPC root (Level 7 only) |
| NavigationHUD | GameManager or PlayerHelicopter |
