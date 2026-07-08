Title: Simulator architecture — chosen design
Date: 2026-06-14T13:03:17Z
Status: proposed
Author: Danny (Lead / Architect)

Context

We need a maintainable, extensible simulator for the superHeli project that supports deterministic physics, control-loop-grade timesteps, sensor models, and a web-based visualization. The simulator should be easy to iterate on and test, and allow performance scaling when needed.

Decision

Primary language: Python for the simulator core (physics, integrators, world state, sensors). Node (TypeScript/JavaScript) will be the secondary language for I/O, tooling, and the visualization/web UI. The core will be modular: a physics core exposing a small, well-documented public API, with pluggable integrators and model plugins. Low-level performance hotspots will be isolated and implemented as native extensions (C/C++/Rust) or via NumPy-vectorized code.

Rationale

- Python provides mature numerical libraries (numpy, scipy), excellent testing and prototyping speed, and a large ecosystem for scientific computing. It reduces development time for algorithm experiments and validation.
- Node is better for real-time web visualization, developer tooling, and packaging a browser-based UI. Keeping simulation core in Python preserves numerical fidelity; Node consumes simulation state over a simple websocket/JSON protocol for visualization and interactivity.
- Using native extensions (C or Rust) for hot paths preserves flexibility and performance. This hybrid approach allows an initial fast MVP in Python and later targeted optimization.

Alternatives considered

1) Node-first (JS) simulator core
  - Pros: single-language stack if frontend is JS; easy reuse of code across UI and server tooling.
  - Cons: weaker numerical ecosystem, harder to write high-quality integrators and vectorized workloads; more effort to reach high performance.

2) Rust-first core with bindings for Python/Node
  - Pros: best raw performance and safety; good for long-term maintainability at scale.
  - Cons: longer development ramp, higher complexity to iterate and test physics models quickly; harder for domain scientists to contribute.

3) Mixed-language microservices (each subsystem in best language)
  - Pros: best tool for each job.
  - Cons: early complexity, deployment overhead, harder to keep deterministic simulation loop.

Trade-offs

- Choosing Python prioritizes developer velocity and ease of verification over upfront raw performance. We accept that some hotspots may need native acceleration later.
- Keeping visualization/UI in Node trades language heterogeneity for separation of concerns: UI can evolve independently and scale to many clients.

Consequences

- Immediate: fast iteration, easy to test and validate models. Clear API contract between core and visualization via JSON-over-websocket or gRPC (protobuf schema recommended for stability).
- Future: when performance limits are hit, we will implement critical components as native extensions or a Rust core with Python bindings. This is an explicit, manageable migration path.

Next steps

1. Create docs/simulator-architecture.md describing the public API, file layout and roadmap.
2. Implement the minimal Python simulator scaffold (world state, integrator interface, simple helicopter model, CLI harness).
3. Provide a small Node visualization stub that connects to the simulator over a simple websocket message format (JSON schema documented in docs).

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
