# Squad Decisions

## Active Decisions

### All GUI strings must be localized via Loc.Get()

**Date:** 2026-06-30  
**Status:** Active

Never write a hardcoded user-facing string in GUI code. Always use `Loc.Get("key")` from `SimCore/Loc.cs`.

**How to add a new string:**
1. Add the key + English value to `EnglishStrings` in `Assets/Editor/LocalizationSetup.cs`
2. Use `Loc.Get("your.key")` at the call site (or `Loc.Get("your.key", arg)` for formatted strings — the table entry uses `{0}`, `{1}` placeholders)
3. Run **Window → SuperHeli → Setup Localization Tables** in the Unity Editor

**Key convention:** `category.name` — e.g. `menu.quit`, `instr.land`, `hud.in_zone`  
**Table name:** `"UI"` (package: com.unity.localization 1.3.2)

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
