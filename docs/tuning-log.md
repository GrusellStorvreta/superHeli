# Simulation mode tuning log

Running log of Simulation-mode physics tuning sessions. Each entry follows the same
structure so sessions are comparable over time. Newest entry first.

Data comes from `BlackBoxRecorder` (`Assets/Scripts/SimCore/BlackBoxRecorder.cs`):
attach it next to `SimulatorDriver` on the helicopter, press Play. It samples at 10 Hz
and appends a CSV every 10 s to `~/Library/Application Support/DefaultCompany/superHeli/BlackBox/`
(only while `GameSettings.SimulationMode` is on). Columns:

```
t, pitch, roll, heading, yawRate, vHorizontal, vVertical, sideslip, collective, cyclicX, cyclicY, pedal
```

`sideslip` is the angle between where the nose points and the direction the helicopter
is actually moving — the main signal for "does this turn feel coordinated."

## Session template

```
## YYYY-MM-DD — <what we were tuning>

**Goal:** why we're looking at this.

**Black box files:**
- `flight_<timestamp>.csv` — what this run covers

**Findings:** what the data showed, with the numbers.

**Changes made:** config fields touched, old → new value, and which file/line.

**Next steps:** what to re-fly / re-check next session.
```

---

## 2026-07-20 — Weathervane oscillation on turns

**Goal:** Simulation mode felt "strange" when turning while holding forward velocity.
Couldn't tell if this was untuned parameters or the pilot's (Anders') own lack of
coordinated-turn technique — no real-world flying experience to calibrate against.
Built `BlackBoxRecorder` this session specifically to answer that.

**Black box files:**
- `flight_20260720_134318.csv`, `flight_20260720_134355.csv`, `flight_20260720_134445.csv`
  — header only, quick Play-mode restarts before the real test run.
- `flight_20260720_134504.csv` — baseline run. Liftoff, crash+reset at `t=31.2s`
  (heading snapped 139.5°→127.0° back to spawn), liftoff again, crash+reset at
  `t=57.2s`, liftoff for good, sustained flight with turns from `t=90s`–`210s`
  (bank up to ~50°, speed up to ~64 m/s), gentle descent/landing to end of log at
  `t=439.9s`.

**Findings:**
- During turns (`|yawRate| > 3°/s`), mean `|sideslip|` = 19.3°, max 148°.
- Even flying "straight" (`|yawRate| ≤ 1°/s`), mean `|sideslip|` = 13.3°, max 174°.
- Isolated piloting vs. physics by splitting turning samples on pedal input: hands-off
  (`|pedal| < 0.05`, physics-only) mean `|sideslip|` = 20.8° — actually *worse* than
  with active pedal (17.2°). Pedal direction was a coin flip between correcting and
  fighting the slip (244 vs. 237 samples). Conclusion: not primarily a piloting issue —
  the hands-off weathervane self-coordination isn't working.
- Raw yaw rate flipped sign abruptly rather than settling — consistent with an
  underdamped spring: the vane torque (`Simulator.cs`, `weathervaneTorque` term) reacts
  to sideslip angle but had nothing damping its own response, so it overshoots past
  zero sideslip and oscillates.

**Changes made:**
- Added `weathervaneDamping` to `SimulatorConfig` (`Simulator.cs:35`), default `0.05`
  deg/s² per (m/s · deg/s yaw rate).
- Vane torque calc in `ComputeTorque` (`Simulator.cs:238-244`) now subtracts
  `weathervaneDamping * hSpeed * yawRateDeg` before clamping to `weathervaneMax`.

**Next steps:** Re-fly a similar turn (hands mostly off pedal, same as baseline) and
compare sideslip stats against this session's baseline numbers above. Tune
`weathervaneDamping` up/down depending on whether yaw rate still oscillates or now
feels sluggish to settle.
