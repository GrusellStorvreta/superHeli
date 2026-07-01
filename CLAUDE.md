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

