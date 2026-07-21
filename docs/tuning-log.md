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

## 2026-07-20 — Weathervane still too weak for cyclic-only turns

**Goal:** After the earlier weathervane-authority bump (`0.02→0.035`), still felt like
more slip than turn on a cyclic-only turn (no pedal) unless giving full pedal too.
Check the black box for the next test flight.

**Black box files:**
- `flight_20260720_171114.csv` — normal takeoff, climb to cruise, straight flight,
  then banking turns. One crash at `t=128.2s` (unrelated to this investigation).

**Findings:**
- Cyclic-only (pedal ~0, `n=355`): mean `|roll|` 12.1°, mean `|yawRate|` **2.37°/s**,
  mean `|sideslip|` 14.1° → 0.196°/s of turn per degree of bank.
- Full pedal (`|pedal|≥0.9`, `n=195`): mean `|roll|` 15.4°, mean `|yawRate|`
  **5.21°/s**, mean `|sideslip|` 15.1° → 0.338°/s of turn per degree of bank — nearly
  double the cyclic-only rate for similar sideslip.
- Concrete example, `t=206.7–209.3s`: 28° bank at 55 m/s (coordinated rate would be
  ~5.5°/s, and the first sample hits `-6.37°/s` — good). But within 0.6s, with bank
  still at 26-28° and sideslip unchanged around `-10°`, `yawRate` collapses to
  `-0.8 to -1.3°/s` and stays there — the turn dies quickly even though both bank and
  slip persist.
- Diagnosis: `weathervaneDamping` (bumped to `0.065` alongside the last torque
  increase, specifically to avoid oscillation) is now suppressing the turn before it
  can build up for a given sideslip. Pedal's extra torque pushes through that damping;
  cyclic alone doesn't.

**Changes made:**
- `weathervaneTorque`: `0.035 → 0.05` (`Simulator.cs:43-47`).
- `weathervaneDamping`: `0.065 → 0.078` (`Simulator.cs:48-50`) — scaled by
  `√(0.05/0.035) ≈ 1.2` to keep roughly the same damping ratio as before, so the
  stronger spring shouldn't reintroduce yaw-rate oscillation.

**Next steps:** Re-fly a cyclic-only turn at similar bank/speed. Check whether
`yawRate` now holds up over the course of the turn instead of collapsing within ~1s,
and whether it approaches the with-pedal numbers above (~0.3°/s per degree of bank).
If it's still weak, `weathervaneTorque` can go higher again; if oscillation reappears,
`weathervaneDamping` needs another bump.

---

## 2026-07-20 — Pedal authority out-torqued by collective at speed

**Goal:** During a held turn, yaw rate collapsed (`-4.05°/s → -1.40°/s`) mid-maneuver
even with full pedal and no cyclic/pedal change to explain it. Check whether this is
realistic or a tuning gap.

**Black box files:**
- `flight_20260720_155150.csv` — same log as the two crash investigations above. Dip
  at `t=73.6–75.6s`: `cyclicX=0.00`, `pedal=-1.00` (unchanged) throughout, but
  `collective` climbed `0.50→0.88` and held there for that exact window.

**Findings:**
- Pulling collective to `0.88` (`collectiveDelta=0.76`) generates ~`38 deg/s²` of
  opposing yaw via `rotorReaction`. At this speed (66-72 m/s), `pedalAuthority` was
  already faded to its floor (`pedalAuthorityAtSpeed = 0.3`), so full pedal only gave
  `pedalTorque × 0.3 = 24 deg/s²` — less than the collective pull alone was fighting.
  No amount of pedal could hold the turn.
- Checked whether this is edge-of-envelope: it isn't. `pedalAuthorityMaxSpeed = 20 m/s`
  (39 kt) is a normal cruise speed, and the crossover point is just
  `collectiveDelta > pedalTorque·pedalAuthorityAtSpeed / rotorTorque = 0.48`, i.e. any
  collective above `0.74` past 39 kt out-torques full pedal — an entirely ordinary
  "climbing a bit while cruising" combination, not a stunt.
- Not realistic: real tail rotors are sized with margin to counter max continuous
  power's torque reaction across the speed range (a helicopter that runs out of pedal
  at 88% collective and 40 kt wouldn't be airworthy). The real phenomenon this
  resembles — loss of tail-rotor effectiveness (LTE) — is a *low-speed*, not
  high-speed, hazard, the opposite end of the envelope from where this triggered.
  Conclusion: `pedalAuthorityAtSpeed` was undersized; these numbers had never been
  tuned before.

**Changes made:**
- `pedalAuthorityAtSpeed`: `0.3 → 0.7` (`Simulator.cs:55-60`) — kept above
  `rotorTorque/pedalTorque = 0.625` with margin, so full pedal can always counter a
  full-deflection collective's yaw reaction, no matter the speed. Pedal still gets
  duller at speed (encouraging bank-to-turn, per the original design intent in
  `docs/physics-plan.md`), just not to the point of being mathematically insufficient.

**Next steps:** Re-fly a similar climb-power turn at cruise speed and confirm yaw rate
no longer collapses when pulling collective, and that pedal still feels appropriately
less crisp than at low speed (not identical to hover authority).

---

## 2026-07-20 — Weak weathervane authority ("mycket slip och lite turn")

**Goal:** Cruising and turning with cyclic only (no pedal) produced a lot of slip but
little actual turn. Check the black box for what's happening.

**Black box files:**
- `flight_20260720_153347.csv` — cruise flight with cyclic-only turns under
  investigation.

**Findings:**
- Cyclic-only banking (pedal ~0, `n=572` samples): mean `|roll|` 18.2°, but mean
  `|yawRate|` only **3.07°/s** against mean `|sideslip|` **25.1°**. With pedal
  assisting (`n=261`): mean `|roll|` 24.3°, mean `|yawRate|` 8.38°/s, mean
  `|sideslip|` drops to 15.9° — pedal is compensating for a real shortfall in the
  hands-off case, not just adding noise.
- Concrete example: sustained ~24° bank at `t=49.4–50.9s`, 22-23 m/s, pedal at 0 —
  `yawRate` only `-4 to -5.5°/s` while `sideslip` grows to `-37° to -41°` and stays
  there for as long as the bank is held.
- Mechanism: a 24° bank at 23 m/s curves the flight path at `g·tan(roll)/v ≈ 11°/s`
  regardless of nose direction — that's just banked-lift geometry. The nose was only
  catching up at ~5°/s (driven by `weathervaneTorque`/`weathervaneDamping`), so the
  gap between where the nose points and where the helicopter is actually going
  (sideslip) sits large for the whole bank instead of closing. Weathervane authority
  was undersized relative to how fast a bank actually curves the path.

**Changes made:**
- `weathervaneTorque`: `0.02 → 0.035` deg/s² per (m/s · deg sideslip)
  (`Simulator.cs:43-46`).
- `weathervaneDamping`: `0.05 → 0.065` deg/s² per (m/s · deg/s yaw rate)
  (`Simulator.cs:47-49`) — bumped roughly proportionally (`√(new/old torque)`) so the
  stronger spring doesn't reintroduce the oscillation fixed earlier this session.

**Next steps:** Re-fly a cyclic-only turn at similar bank/speed and check the black
box: `yawRate` should track closer to the bank-implied turn rate and `sideslip`
should stay smaller and/or resolve faster. If it still lags, `weathervaneTorque` can
go higher; if turns start feeling twitchy/oscillating again, damping needs another
bump.

---

## 2026-07-20 — Lateral slide when transitioning climb to cruise

**Goal:** Climbing to altitude then pushing into forward cruise caused the helicopter
to slide sideways even while actively compensating yaw with the pedals. Real behavior
or a tuning artifact?

**Black box files:**
- `flight_20260720_150553.csv` — climb then cruise transition under investigation.

**Findings:**
- Climb (`t=14–21s`): collective at `1.00`, pedal working hard (`-0.2` to `-0.76`
  repeatedly) to hold heading against main-rotor torque reaction. Heading still
  drifted `97.3°→110.4°` (~13°) despite continuous correction.
- Cruise transition (`t=21–29s`): pitching forward to accelerate (~18-19° nose-down)
  while pulling collective again (peaking `0.99–1.00`). `roll` climbed `0°→4.7°+`
  while `cyclicX` stayed at `0.00` the entire time — no roll input at all. `sideslip`
  grew to `-21.8°` over the same window, with pedal now slamming `-1.00↔0.00`
  reactively.
- Mechanism: pulling collective creates a real, fairly large yaw disturbance
  (`rotorReaction` in `Simulator.cs`) that pedal must chase. Yaw torque applied while
  pitched forward couples into roll — genuine 3D rigid-body kinematics (rotating
  around a tilted "up" axis when nose-down partially reads out as roll), not a code
  bug. Real helicopters have a version of this too. The amplifying factor is that the
  yaw disturbance from the collective pull was large and reactive (sharp pedal stabs)
  rather than smooth, so the coupled-roll impulses were large too.

**Changes made:**
- Added `rotorTorqueDamping` (default `0.4` deg/s² per deg/s yaw rate) to
  `SimulatorConfig` (`Simulator.cs:19-23`).
- `rotorReaction` in `ComputeTorque` (`Simulator.cs:232-240`) now subtracts
  `rotorTorqueDamping * yawRateDeg`, smoothing the yaw kick from a power change so it
  needs steady pedal instead of sharp reactive stabs. Steady-state pedal requirement
  for a sustained collective offset is unchanged (damping only smooths the transient);
  `yawRateDeg` is now computed once and shared with the weathervane term too.

**Next steps:** Re-fly the same climb-then-cruise transition and check the black box
for less pedal-stabbing and less unintended roll/sideslip during the transition. Tune
`rotorTorqueDamping` up if pedal still feels jerky, or down if yaw now feels sluggish
to respond to collective changes.

---

## 2026-07-20 — Flapback runaway on cyclic release

**Goal:** At speed, transitioning from forward flight to neutral cyclic caused the
helicopter to pitch up heavily and crash. Same question as the weathervane session:
real behavior or a tuning artifact?

**Black box files:**
- `flight_20260720_145328.csv` — flight ending in the crash under investigation.

**Findings:**
- From `t=151.0s` to `t=154.6s`, `cyclicX`/`cyclicY` sit at `0.00` (stick centered)
  while `pitch` goes from `-4.4°` to `-43.1°` (nose-up) and `vHorizontal` decays
  `54.9→27.4 m/s`. Crash (ground contact) at `t=154.7s`.
- Real basis: forward-airspeed flapback (nose wants to pitch up, requiring forward
  cyclic to hold cruise attitude) is a genuine helicopter effect.
- Not realistic: the magnitude/runaway. `Simulator.cs`'s flapback term was an
  uncapped, undamped torque proportional only to speed (`-fwdSpeed * flapbackTorque`,
  ~-38.5 deg/s² at 50 m/s), opposed only by the generic `angularDrag = 2.5`, so
  releasing the stick at speed produced a sustained ~15°/s pitch-up rather than a
  gentle, correctable tendency — same shape of bug as the weathervane oscillation
  (strong open-loop torque, no damping/cap of its own).

**Changes made:**
- Added `flapbackDamping` (default `0.3` deg/s² per (m/s · deg/s pitch rate)) and
  `flapbackMax` (default `60` deg/s² cap) to `SimulatorConfig` (`Simulator.cs:32-37`).
- Flapback calc in `ComputeTorque` (`Simulator.cs:233-242`) now subtracts
  `flapbackDamping * fwdSpeed * pitchRateDeg` before clamping to `flapbackMax`. At
  steady state this settles releasing-the-stick pitch rate to roughly
  `flapbackTorque / flapbackDamping ≈ 2.6°/s` instead of ~15°/s.

**Next steps:** Re-fly the same high-speed cyclic-to-neutral transition and check the
black box for a much gentler pitch-up instead of a runaway into the ground. Tune
`flapbackDamping` if it still feels too aggressive or now feels too flat/lifeless.

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

**Follow-up (same day):** Re-flown in `flight_20260720_144611.csv` (140s). Turning
mean `|sideslip|` 19.3°→**10.0°**, max 148°→**31.8°**; straight-flight max
174°→**36.4°**; hands-off turning mean 20.8°→**12.4°**, confirming the fix (not just
less pedal-fighting); yaw-rate sign-flip rate 3.19/10s→**2.43/10s**. Damping is
working — extreme sideslip spikes are gone. Sideslip isn't at zero, so
`weathervaneDamping` could still be nudged up if a future session wants tighter
coordination, but it's no longer the main issue.
