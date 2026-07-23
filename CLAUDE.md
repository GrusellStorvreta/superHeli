This is a Unity helicopter simulator for MacOS. 

Core scripts: 
- Simulator.cs: handles physics 
- SimulatorDriver.cs: input + updates 
- HelicopterPlayer.cs: visuals only 

Goals: - realistic but simple helicopter physics 
- stable hover training gameplay 


Constraints: - no external physics engine 
- keep code simple and readable

## Localization

All user-facing strings must use the Unity Localization package (com.unity.localization 1.3.2).
Never hardcode GUI strings — always go through `Loc.Get("key")` in SimCore/Loc.cs.

Pattern:
- Simple string:  `Loc.Get("menu.quit")`
- With arguments: `Loc.Get("result.continue", _nextLevel)`  →  format arg `{0}` in table entry

When adding a new string:
1. Add the key + English value to the `EnglishStrings` dictionary in `Assets/Editor/LocalizationSetup.cs`
2. Use `Loc.Get("your.key")` at the call site
3. Run **Window → SuperHeli → Setup Localization Tables** in the Unity Editor to regenerate the table

String table name: `"UI"` (Assets/Localization/)
Keys follow dot-notation: `category.name` (e.g. `menu.quit`, `instr.land`, `hud.in_zone`)

### First-time setup (required once per machine / fresh clone)

The `Assets/Localization/` folder and its contents are generated assets — not committed to git.
After cloning or if you see `SelectedLocale is null` errors:

1. In the Unity Editor, run **Window → SuperHeli → Setup Localization Tables**
   This creates `LocalizationSettings`, the English locale, and the "UI" string table.
2. Verify in **Window → Asset Management → Localization Settings** that English appears
   under *Available Locales* and is selected as the active locale.
3. Press Play — strings should resolve correctly.

`Loc.Get()` falls back to returning the raw key if the locale isn't ready,
so the game remains functional even without localization set up.

## Audio

### Architecture

Audio routing uses Unity's **Audio Mixer** (`Assets/MainMixer`).  
The mixer has two groups under Master:

| Group | Sources |
|---|---|
| `Interior` | Engine interior, speech, radio crackle |
| `Exterior` | Engine exterior |

`CameraLook` switches between two **snapshots** (`Interior` / `Exterior`) when the player cycles cameras. Each snapshot silences the other group (−80 dB). Do not use `.mute` on AudioSources for camera switching — the snapshots handle it.

### Speech system (SpeechPlayer)

`SpeechPlayer` maps string tokens to arrays of `AudioClip`. Call `Say("token")` to play a random variant. Mixer routing (Interior group) automatically silences speech in exterior view.

**Adding new speech variants:**
- Add an entry in `SpeechPlayer.entries` in the Inspector (token + clip array)
- Tokens are internal identifiers, not localized strings

**Current tokens used by FlightInstructor:**

| Token | When |
|---|---|
| `landing.success` | Successful landing |

`SpeechPlayer` also adds a high-pass filter and dynamic distortion filter automatically in `Awake` to simulate radio audio. Tweak `High Pass Cutoff`, `Base Distortion`, and `Max Distortion` in the Inspector.

### First-time setup (required once per machine / fresh clone)

The `MainMixer` asset **is** committed to git, but AudioSource Output assignments live in the scene.  
After cloning, verify in each scene that:

1. `CameraLook` has `Interior Snapshot` and `Exterior Snapshot` assigned
2. All AudioSources have the correct **Output** mixer group set:
   - Engine interior → `Interior`
   - Engine exterior → `Exterior`  
   - Speech / crackle AudioSources on FlightInstructor → `Interior`
3. `FlightInstructor.Speech` references the `SpeechPlayer` component

## Simulation mode tuning

Simulation mode's flight model (`SimulatorConfig` in `Assets/Scripts/SimCore/Simulator.cs`)
gets tuned iteratively against recorded flight data, since neither of us is a licensed
helicopter pilot to calibrate against by feel alone.

**Tools:**
- `Assets/Scripts/SimCore/BlackBoxRecorder.cs` — attach next to `SimulatorDriver` on the
  helicopter. Samples attitude/rates/velocity/controls at 10 Hz, appends a CSV every 10 s
  to `~/Library/Application Support/DefaultCompany/superHeli/BlackBox/` (only while
  `GameSettings.SimulationMode` is on). Key column: `sideslip` — angle between nose
  heading and actual travel direction; near 0 = coordinated turn.
- `docs/tuning-log.md` — dated log of tuning sessions (goal, black box files used,
  findings, config changes, next steps). Every tuning session gets a new entry there
  following the template at the top of the file.

When starting a new tuning session: check `docs/tuning-log.md` for the most recent
entry and open questions before making changes.

## AI flight coach & Lesson mode

### Architecture

`ICoachOutput` is the seam between coach analysis and however a comment reaches
the player (`Say(string)`). Two producers, two displays, sharing that seam:

| Component | Role |
|---|---|
| `FlightCoachAI` | Passive coach — every `chunkInterval` seconds, sends the last buffered telemetry chunk to Claude for general commentary. |
| `FlightCoachTester` | Cycles canned phrases through `ICoachOutput` — no API calls, for testing the display pipeline for free. |
| `FlightTaskCoachAI` | Lesson-mode coach — cycles a sequential, in-code list of maneuvers, each with its own timing window; buffers telemetry for that window, then asks Claude to judge the attempt against the *assigned* task. |
| `HudCoachOutput` | Bottom-left amber panel. Displays whatever `Say()` is called with (coach commentary or task feedback). Panel height auto-sizes to the message (`minPanelHeight`/`maxPanelHeight`), so keep replies short. |
| `LessonHUD` | Top-center bar (mission-instruction style) showing `FlightTaskCoachAI.CurrentTaskText` + `TimeRemaining`. Self-gates on `coach.enabled` — inert outside Lesson mode. |

Both AI coaches only sample/send while `GameSettings.SimulationMode` is on
(Arcade physics isn't what real telemetry coaching should be judging), and both
require `ANTHROPIC_API_KEY` in the environment — missing it disables the
component with a `Debug.LogWarning`, not a crash. GUI apps on macOS (Unity
Editor launched from Dock/Finder) don't inherit shell-profile exports — launch
the Editor from a terminal where the var is exported, or
`launchctl setenv ANTHROPIC_API_KEY "..."` to reach GUI-launched apps too.

**Model:** both default to `claude-haiku-4-5-20251001`. Do **not** add an
`output_config`/`effort` field to the request — that parameter is only
supported by reasoning-capable models (e.g. Opus); on Haiku it's an
HTTP 400 (`"This model does not support the effort parameter"`).

**Logging:** both coaches append plaintext transcripts under
`~/Library/Application Support/DefaultCompany/superHeli/BlackBox/Coaching/`
(sibling to `BlackBoxRecorder`'s CSV flight logs) — `coaching_<timestamp>.txt`
for `FlightCoachAI`'s running commentary, `tasks_<timestamp>.txt` for
`FlightTaskCoachAI`'s task/feedback pairs.

### Lesson mode

`GameSettings.Mode` gained a third value, `Lesson`, alongside `FreeFlight` and
`Mission` — a menu entry between Missions and Settings. It reuses Free
Flight's open world (no checkpoints/timer), but `MainMenuManager` hands the
shared `HudCoachOutput` to `FlightTaskCoachAI` for the duration: whichever
passive coach (`FlightCoachAI`/`FlightCoachTester`) is enabled gets suspended
on entry and restored to its prior enabled state on exit, rather than forced
back on.

Current task list (sequential, defined in-code in `FlightTaskCoachAI.cs` —
an Inspector-editable/reorderable list is a natural next step if needed):

| id | Task | Window |
|---|---|---|
| `turn_180_left` | Come to 80 knots, then make a banking turn 180° to the left | 45s |
| `hover_20s` | Come to a hover and maintain it for 20 seconds | 35s |
| `level_70kt_20s` | Come to 70 knots and maintain level flight for 20 seconds | 30s |

### Wiring checklist (per scene, after adding/changing coach components)

1. On the helicopter GameObject (same one as `SimulatorDriver`): add
   `FlightCoachAI` and/or `FlightCoachTester` (passive coach), and
   `FlightTaskCoachAI` (Lesson coach) — both require `SimulatorDriver`.
2. Somewhere in the scene: `HudCoachOutput` (bottom feedback panel) and
   `LessonHUD` (top task bar).
3. On `MainMenuManager`, under **Lesson mode**: **Lesson Coach** →
   `FlightTaskCoachAI`, **Lesson HUD** → `LessonHUD`, **Passive Coach
   Components** → `FlightCoachAI`/`FlightCoachTester`.
4. Run **Window → SuperHeli → Setup Localization Tables** after adding new
   Loc keys (`menu.lesson`, `task.*`).

