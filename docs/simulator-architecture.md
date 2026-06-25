# SuperHeli — Architecture

SuperHeli is a Unity-based helicopter simulator and game. This document describes the current code architecture, module responsibilities, data flow, and extension points.

---

## Technology Stack

| Layer | Technology |
|---|---|
| Engine | Unity (HDRP) |
| Language | C# (.NET, Unity runtime) |
| Input | Unity Input System (new) |
| Physics | Unity Rigidbody + custom force model |
| Audio | Unity AudioSource / AudioListener |
| Persistence | Unity PlayerPrefs |
| Networking | NativeWebSocket (optional, for external control) |

---

## High-Level Module Map

```
┌─────────────────────────────────────────────────────┐
│                     Unity Scene                     │
│                                                     │
│  ┌──────────────┐   ┌──────────────────────────┐   │
│  │  Input Layer │   │      Mission System       │   │
│  │  HeliInput   │   │  MissionManager           │   │
│  │  SimDriver   │   │  MissionHUD               │   │
│  └──────┬───────┘   │  CheckpointRing           │   │
│         │           │  RescueNPC                │   │
│  ┌──────▼───────┐   └──────────────────────────┘   │
│  │ Physics Core │                                   │
│  │  Simulator   │   ┌──────────────────────────┐   │
│  │  WindForce   │   │       UI / HUD            │   │
│  └──────┬───────┘   │  MainMenuManager          │   │
│         │           │  PauseManager             │   │
│  ┌──────▼───────┐   │  SimulatorHUD             │   │
│  │   Visuals    │   │  NavigationHUD            │   │
│  │  HeliPlayer  │   └──────────────────────────┘   │
│  │  CameraLook  │                                   │
│  │  EngineAudio │   ┌──────────────────────────┐   │
│  └──────────────┘   │        NPC System         │   │
│                     │  NPCPatrol                │   │
│  ┌──────────────┐   └──────────────────────────┘   │
│  │   Landing    │                                   │
│  │ LandingCheck │   ┌──────────────────────────┐   │
│  │ CrashHandler │   │     Settings / State      │   │
│  └──────────────┘   │  GameSettings (static)    │   │
│                     └──────────────────────────┘   │
└─────────────────────────────────────────────────────┘
```

---

## Module Descriptions

### Physics Core

**`Simulator.cs`** — Pure C# class (no MonoBehaviour). Owns the helicopter dynamics model:
- Actuator lag filters on collective, cyclic, pedal inputs (first-order filters)
- `ComputeForce()`: returns lift vector + linear drag
- `ComputeTorque()`: returns control torques + angular damping
- Stateless per call — all state held in `ActuatorState` per entity ID
- No Unity dependencies — can be unit tested in isolation

**`SimulatorDriver.cs`** — MonoBehaviour bridge between `Simulator` and Unity's Rigidbody:
- Reads smoothed/deadzoned input from `HeliInput`
- Calls `Simulator.UpdateActuators()` and `Simulator.ComputeForce/Torque()` in `FixedUpdate`
- Applies results via `rb.AddForce()` / `rb.AddTorque(ForceMode.Acceleration)`
- Owns spawn/reset logic (`ResetToSpawnPoint`, `ApplyReset`)
- Exposes `LastBufferedControl`, `LastBodyPosition/Rotation/Velocity` as read-only properties

**`WindForce.cs`** — Reads active `WindZone` and applies aerodynamic push force:
- Formula: `rb.AddForce(windVelocity * dragCoefficient)`
- Combined with Simulator's existing drag this equals `(wind - velocity) * coeff`
- Caches WindZone reference in `Awake` (not per frame)
- Zero overhead when `windMain == 0`

**`SimulatorConfig`** (inner struct in Simulator.cs) — Physics constants:
- `mass`, `maxLift`, `linearDrag`, `angularDrag`, `cyclicTorque`, `pedalTorque`
- Actuator time constants: `tau_collective`, `tau_cyclic`, `tau_pedal`

---

### Input Layer

**`HeliInput.cs`** — Normalises raw device input to simple float properties:
- Loads `HeliControls.inputactions` from Resources if present
- Falls back to `Gamepad.current` then `Keyboard.current`
- Exposes: `move (Vector2)`, `collective (float)`, `yaw (float)`, `resetPressed (bool)`

**`SimulatorDriver.cs`** also handles keyboard overrides (arrow keys, A/Z, N/M) on top of HeliInput, with exponential smoothing and deadzone filtering.

---

### Visuals

**`HelicopterPlayer.cs`** — Visual-only MonoBehaviour on the helicopter:
- Spins rotor mesh based on `LastBufferedControl.collective`
- No physics logic

**`CameraFollow.cs`** — Smooth third-person follow camera:
- Uses yaw-only offset to prevent oscillation during banking turns
- `LateUpdate` with `Vector3.Lerp` + `Quaternion.Slerp`

**`CameraLook.cs`** — Manages camera switching and audio:
- Holds `Camera[]` array; cycles with C key / gamepad South button
- Camera order: `[0]` FPV (interior), `[1]` Follow (exterior), `[2]` LookDown (interior)
- Mutes/unmutes `exteriorAudio` and `interiorAudio` based on active camera index

**`EngineAudio.cs`** — Drives AudioSource pitch and volume from collective:
- Reads `driver.LastBufferedControl.collective`
- Exponential smoothing (`smoothingTime = 0.5s`)
- Applies to both exterior and interior AudioSources

---

### Landing & Safety

**`LandingChecker.cs`** — Validates landings on `OnCollisionEnter`:
- Checks vertical speed, horizontal speed, tilt angle, ground flatness
- Fires `public event Action<LandingResult> OnLanding`
- `LandingResult` struct: `{ bool success, string failReason }`

**`CrashHandler.cs`** — Subscribes to `LandingChecker.OnLanding`:
- Failed landing → fade-to-black coroutine → `ResetToSpawnPoint()`
- Successful landing → brief "Nice landing!" text overlay

---

### Mission System

**`MissionManager.cs`** — State machine that runs a level's task sequence:

Task types (`TaskDef.Kind`):
| Kind | Description |
|---|---|
| `ClimbToAGL` | Reach target altitude above ground |
| `HoverAtAGL` | Hold position within ±deviation for N seconds |
| `FlyThrough` | Fly through a `CheckpointRing` trigger |
| `NavigateTo` | Reach a world Transform within radius |
| `PickupNPCs` | Land near `RescueNPC` objects to board them |
| `Land` | Complete a successful landing |

State machine: `Idle → Running → Success / Failed`

- Failed: waits 3s → resets helicopter → restarts current level
- Success: unlocks next level in `GameSettings`, waits 4s → returns to menu (or loads `returnSceneName` for scene-based levels)
- Exposes `NavigationTarget (Vector3?)` for `NavigationHUD`
- Exposes `HoverProgress`, `HoverInZone`, `IsHoverTask` for `MissionHUD`

**Level definitions** (hardcoded methods, easy to extend):
- `BuildLevel1()` — Climb 100ft, hover 5s, land. 30s limit.
- `BuildLevel2()` — Climb 200ft, hover, two checkpoint rings, land. 120s limit.
- `BuildLevel7()` — Navigate to rescue zone, pick up survivors, return to base. 120s limit, wind enabled.

**`MissionHUD.cs`** — OnGUI overlay during missions:
- Task instruction bar + countdown timer (amber → red under 10s)
- Hover progress bar (green = in zone, red = out of zone)
- Success/fail full-screen result overlay

**`CheckpointRing.cs`** — Visual ring + trigger for FlyThrough tasks:
- `LineRenderer` draws amber circle (turns green when passed)
- `SphereCollider` (trigger) detects Rigidbody entry
- Fires `event Action OnPassedThrough`
- Starts disabled; `MissionManager` activates rings one at a time

**`RescueNPC.cs`** — Boardable survivor for Level 7:
- Polls helicopter AGL and horizontal distance each frame
- When helicopter lands within `pickupRadius` → `Board()`:
  - Sets NPC as child of helicopter transform
  - Disables `NPCPatrol`
  - Calls `driver.AddPassengerMass(passengerMassKg)`
- `Unboard()` reverses this on crash/reset

**`NavigationHUD.cs`** — Bearing + distance panel:
- Reads `MissionManager.NavigationTarget`
- Calculates world bearing and relative bearing from helicopter heading
- Draws bearing degrees, distance, and a procedural arrow (rotated with `GUIUtility.RotateAroundPivot`)
- Hidden when `NavigationTarget == null`

---

### UI

**`MainMenuManager.cs`** — Main menu controller:

Menu phases:
- `Main` — FREE FLIGHT, MISSION MODE, SETTINGS, QUIT
- `LevelSelect` — LEVEL 1, LEVEL 2, LEVEL 7 (locked until unlocked), BACK
- `Settings` — SOUND toggle, volume slider, BACK

- Freezes helicopter (`isKinematic = true`, driver disabled) during menu
- Activates `MenuCamera`, deactivates gameplay cameras
- Sets `GameSettings.CurrentLevel` and enables correct components on game start
- Scene-loads `"Level7"` for Level 7 via `SceneManager.LoadScene`
- Full gamepad navigation (D-pad + A/B buttons)

**`PauseManager.cs`** — In-game pause:
- P key or gamepad Start button toggles pause
- `Time.timeScale = 0` while paused
- OnGUI overlay: RESUME, MAIN MENU
- Main Menu option reloads current scene (cleanly resets to menu state)

**`SimulatorHUD.cs`** — Flight instrument panel (OnGUI):
- Heading, airspeed (kt), vertical speed (fpm), MSL altitude (ft), AGL altitude (ft)
- Attitude gimbal with crosshair (procedural `Texture2D`)

---

### NPC System

**`NPCPatrol.cs`** — Waypoint-based patrol state machine:
- States: `Idle → Starting → Walking → Stopping → Idle`
- Drives `Animator` via `bool isWalking` parameter
- `MoveTowards` + rotation `Slerp` for smooth locomotion
- Configurable idle wait time (random range), walk speed, waypoints

---

### Settings & State

**`GameSettings.cs`** — Static class, persisted via `PlayerPrefs`:

| Property | Storage key | Default |
|---|---|---|
| `CurrentMode` | — (runtime only) | FreeFlight |
| `CurrentLevel` | — (runtime only) | 1 |
| `UnlockedLevels` | `"UnlockedLevels"` | 1 |
| `SoundEnabled` | `"SoundEnabled"` | true |
| `SoundVolume` | `"SoundVolume"` | 1.0 |

`ApplyAudioSettings()` sets `AudioListener.volume` globally.

---

## Data Flow

```
HeliInput
    │ move, collective, yaw
    ▼
SimulatorDriver.Update()
    │ smoothing + deadzone
    ▼
Simulator.SetControlInput()
    │
SimulatorDriver.FixedUpdate()
    ├── Simulator.UpdateActuators()   (actuator lag filter)
    ├── Simulator.ComputeForce()  ──► rb.AddForce()
    └── Simulator.ComputeTorque() ──► rb.AddTorque()

WindForce.FixedUpdate()
    └── WindZone values ──► rb.AddForce()

MissionManager.Update()
    ├── EvaluateTask() ──► task conditions
    ├── NavigationTarget ──► NavigationHUD
    └── HoverProgress / IsHoverTask ──► MissionHUD

LandingChecker.OnCollisionEnter()
    └── OnLanding event ──► CrashHandler
                       └──► MissionManager
```

---

## Extension Points

**Adding a new level:**
1. Add `BuildLevelN()` method in `MissionManager.cs` following existing pattern
2. Add a TaskDef array using existing `Kind` values (or add a new Kind + evaluation case)
3. Add button in `MainMenuManager.DrawLevelSelect()` with unlock condition
4. If new scene needed: create Unity scene, set `returnSceneName`, register in Build Settings

**Adding a new task type:**
1. Add value to `TaskDef.Kind` enum
2. Add fields to `TaskDef` class
3. Add case to `EvaluateTask()` switch
4. Update `NavigationTarget` in `AdvanceTask()` if navigable
5. Update `MissionHUD` if new visual feedback needed

**Adding wind to a level:**
Set `LevelEnvironment.windSpeed > 0` in the level's `BuildLevelN()`. `MissionManager.ApplyEnvironment()` writes the values to the scene's `WindZone` automatically.

---

## File Layout

```
Assets/Scripts/SimCore/
├── Simulator.cs           Physics model (pure C#, no Unity deps)
├── SimulatorDriver.cs     Rigidbody bridge + input processing
├── HeliInput.cs           Input abstraction
├── HelicopterPlayer.cs    Rotor animation
├── WindForce.cs           Wind aerodynamics
├── LandingChecker.cs      Landing validation
├── CrashHandler.cs        Crash/respawn sequence
├── CameraFollow.cs        Third-person camera
├── CameraLook.cs          Camera switching + audio routing
├── EngineAudio.cs         Engine sound driven by collective
├── MissionManager.cs      Mission state machine + level definitions
├── MissionHUD.cs          Mission overlay UI
├── CheckpointRing.cs      Fly-through ring trigger + visual
├── RescueNPC.cs           Boardable survivor NPC
├── NavigationHUD.cs       Bearing + distance display
├── NPCPatrol.cs           NPC waypoint patrol
├── MainMenuManager.cs     Main menu + level select
├── PauseManager.cs        In-game pause
├── GameSettings.cs        Global settings (PlayerPrefs)
└── SimulatorHUD.cs        Flight instrument HUD
```
