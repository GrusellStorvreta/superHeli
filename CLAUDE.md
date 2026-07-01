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

