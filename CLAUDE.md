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
| `CoachOutputBroadcaster` | Fans a single `Say()` call out to every configured `ICoachOutput` (e.g. `HudCoachOutput` + `TtsCoachOutput`) — the seam `FlightCoachAI`/`FlightTaskCoachAI`/`FlightCoachTester` fall back to instead of `HudCoachOutput` directly. |
| `TtsCoachOutput` | Speaks the coach's comment aloud via the ElevenLabs API, routed through the existing `SpeechPlayer.audioSource` so it gets the identical radio effect (high-pass, dynamic distortion, crackle, Interior mixer group) as pre-recorded phrases — no separate AudioSource or mixer wiring needed. |

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

**Voice (`TtsCoachOutput`):** requires `ELEVENLABS_API_KEY` in the environment
(same macOS GUI-launch caveat as `ANTHROPIC_API_KEY` above) and a **Voice Id**
filled in on the component from your own ElevenLabs account — this cannot be
defaulted or guessed. Missing either disables the component with a
`Debug.LogWarning`, same convention as the Claude coaches. **Use a voice from
"My Voices," not a shared Voice Library voice** — the latter returns HTTP 402
("Free users cannot use library voices via the API") on free-tier accounts;
this is an ElevenLabs account/billing restriction, not a bug in this code.
Uses `eleven_flash_v2_5` (low-latency/cost model) and `pcm_22050` output —
deliberately below the 44.1k/48k PCM/WAV tier gate on ElevenLabs' Pro+ plans.
Text and voice run together, not one replacing the other — `HudCoachOutput`'s
text is synchronous and appears instantly, while the spoken line lags behind
it by the ElevenLabs request's round-trip time; that's expected, not a bug.
Unchecking `TtsCoachOutput`'s **Voice Enabled** field turns voice off (text
keeps working) without touching the broadcaster wiring — a free mute switch
if ElevenLabs cost/latency becomes a concern (this doubles the external API
surface per coaching comment: one Claude call, one ElevenLabs call). Note
this is a dedicated bool field, not the component's own enable/disable
checkbox — `TtsCoachOutput` has no `Update`/`OnGUI`, so Unity doesn't show
that checkbox at all, same reason `CoachOutputBroadcaster` doesn't either.

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
   `FlightTaskCoachAI`, **Lesson HUD** → `LessonHUD`, **Flight Coach AI** /
   **Flight Coach Tester** → whichever passive coach(es) exist on
   PlayerHelicopter (each field resolves that exact component type
   unambiguously, even when dragging the whole GameObject — see note below).
4. Run **Window → SuperHeli → Setup Localization Tables** after adding new
   Loc keys (`menu.lesson`, `task.*`).
5. Add a `CoachOutputBroadcaster` somewhere in the scene. Wire its **Hud
   Output** field to `HudCoachOutput` (always) and **Tts Output** to
   `TtsCoachOutput` (if voice is set up). Required even for text-only setups
   — `FlightCoachAI`/`FlightTaskCoachAI`/`FlightCoachTester` resolve their
   output via `FindObjectOfType<CoachOutputBroadcaster>()`, exclusively; a
   scene without a broadcaster loses coach text entirely, not just voice.

> **Don't put an `ICoachOutput`-implementing component (e.g. `HudCoachOutput`)
> directly on the same GameObject as a coach** (`FlightCoachAI`/
> `FlightTaskCoachAI`/`FlightCoachTester`). Earlier versions of this code
> checked `GetComponent<ICoachOutput>()` on the coach's own GameObject before
> falling back to `FindObjectOfType<CoachOutputBroadcaster>()` — a stray
> `HudCoachOutput` left on `PlayerHelicopter` from before the broadcaster
> existed silently shortcut past it, so voice (and any future outputs) never
> ran even though text kept working, with zero related Console output. That
> `GetComponent` check has been removed — the coaches now *only* resolve via
> `CoachOutputBroadcaster` — so this can't happen again regardless of what
> else ends up on that GameObject.

> **Why explicit fields, not arrays**: `MainMenuManager`'s passive-coach
> field and `CoachOutputBroadcaster`'s output fields are deliberately typed
> as concrete component types (`FlightCoachAI`, `TtsCoachOutput`, etc.), not
> a `Behaviour[]`/`MonoBehaviour[]` array. When several scripts share one
> crowded GameObject (as they do here on `PlayerHelicopter`), dragging the
> whole GameObject onto a broad-typed field resolves to *some* component of
> that type via `GetComponent` — not necessarily the one you meant — since
> the Hierarchy only lets you drag GameObjects, not a specific component.
> This actually happened once already: a `Behaviour[]` field silently grabbed
> `HelicopterPlayer` instead of `FlightCoachAI`, which stopped rotor
> animation specifically in Lesson mode. Concrete-typed fields don't have
> this ambiguity.
6. If adding voice: add `TtsCoachOutput` to the scene, set **Voice Id** (from
   your ElevenLabs account), and confirm **Speech Player** resolves to the
   same `SpeechPlayer` `FlightInstructor` already uses (auto-found via
   `FindObjectOfType<SpeechPlayer>()` if left empty) — using a different one
   would mean the radio effect isn't actually shared/identical.

