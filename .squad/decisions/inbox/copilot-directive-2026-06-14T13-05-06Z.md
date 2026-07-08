### 2026-06-14T13:05:06Z: User directive
**By:** (git user.name not configured locally) (via Copilot)

**What:** No Python — use C# for the simulator core instead of Python.

**Why:** User preference (requested during session). This overrides the prior Danny recommendation to use Python for the simulator core.

**Action:** Coordinator / Lead should update the simulator ADR and implementation plan to make C# the primary language for the simulator core. Keep Node (TypeScript) as the visualization option and reserve native/C/Rust only for extreme hotspots if needed.


Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
