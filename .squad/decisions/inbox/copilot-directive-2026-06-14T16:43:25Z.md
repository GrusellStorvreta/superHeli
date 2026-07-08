### 2026-06-14T16:43:25Z: User directive
**By:** (git user.name not configured locally) (via Copilot)

**What:** Control mapping and realism requirements for simulator input:
- Controller mapping: Xbox controller mapping required.
  - Left stick -> Collective
  - Right stick -> Cyclic (X/Y)
  - Triggers (LT/RT) -> Pedals (yaw differential)
- Realism: introduce actuator/command delays to emulate MD500 behaviour — controls should not be instant; implement configurable time-delay/first-order actuator dynamics for collective, cyclic, and pedals.

**Why:** User requirement for realistic control coupling and input mapping for simulator experiments and testing.

**Action:**
- Basher: implement Xbox controller mapping in Unity SimulatorDriver and send structured control inputs to the simulator (and over websocket). Support legacy Input axes and provide notes for Input System mapping.
- Rusty: implement ControlInput support in the C# Simulator core and apply actuator lag (configurable time constants) using a stable first-order low-pass filter or delay queue. Expose parameters in SimulatorConfig and document effects.

**Notes:** This directive overrides prior ad-hoc input approaches. Record this as a team-level decision so agents respect the mapping and delay behavior.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
