# Rusty — Actuator delay model

Timestamp: 2026-06-14T16:43:25Z
Author: Rusty (Physics Dev)

Summary
-------
Added a simple first-order actuator lag model to Simulator.cs. Each control channel maintains both a target (pilot command) and an applied (lagged) value. The applied value is updated each simulation step with a discrete first-order filter.

Model
-----
For each channel x (collective, cyclic_x, cyclic_y, pedal):
  x_applied_{n+1} = x_applied_n + alpha * (x_target - x_applied_n)
  where alpha = dt / (tau + dt)

Numerical rationale: alpha = dt / (tau + dt) is stable for small dt (alpha -> 0) and produces full-step behaviour for dt >> tau (alpha -> 1). It avoids division by zero and behaves well for variable timesteps.

Default time constants
----------------------
- tau_collective = 0.25 s
- tau_cyclic = 0.15 s
- tau_pedal   = 0.12 s

These defaults were chosen as reasonable mid-range actuator lags for a small helicopter/RC-style servos. They are configurable via SimulatorConfig (fields tau_collective, tau_cyclic, tau_pedal).

How to tune
-----------
- To make actuators faster, reduce the corresponding tau.
- To model sluggish actuators, increase tau.
- Run the built-in self-test by setting SimulatorConfig.runStepResponseTest = true; the simulator will run a short step-response on first Step() and log a compact summary.
- For precise matching against recorded actuator step responses, fit tau to the measured exponential time constant (tau is the continuous-time time constant).

Notes
-----
- The Simulator updates applied controls every Step(dt). Force computation should read the applied values (GetAppliedControl) when implemented.
- The code provides a convenience SetControlInput overload for single-vehicle setups (uses default entity id "default").
