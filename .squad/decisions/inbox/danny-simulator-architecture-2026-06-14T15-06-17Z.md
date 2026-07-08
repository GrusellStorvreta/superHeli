1. Title: Simulator architecture — chosen design (C# core)
2. Date: 2026-06-14T15:06:17Z
3. Status: proposed
4. Author: Danny (Lead / Architect)
5. 
6. Context
7. 
8. We need a maintainable, extensible simulator for the superHeli project that supports deterministic physics, control-loop-grade timesteps, sensor models, and a web-based visualization. The simulator should be easy to iterate on and test, and allow performance scaling when needed. A previous ADR recommended Python for the core; a new directive requires using C# for the simulator core instead.
9. 
10. Decision
11. 
12. Primary language: C# (.NET) for the simulator core (physics, integrators, world state, sensors). Node (TypeScript/JavaScript) remains the visualization and tooling layer. The core will be modular: a physics core exposing a small, well-documented public API (Simulator, Frame classes), with pluggable integrators and model plugins. Low-level performance hotspots should be implemented with modern .NET techniques (Span<T>, hardware intrinsics / SIMD) or, only if necessary, via native interop (C/Rust) or NativeAOT compiled components.
13. 
14. Rationale
15. 
16. - C#/.NET provides strong static typing, mature tooling (VS/VSCode, dotnet CLI), and high single-threaded performance adequate for control-loop rates. .NET has System.Numerics for SIMD-aware types and mature numeric libraries (MathNet.Numerics) for linear algebra and special functions.
17. - Using C# reduces the impedance mismatch between prototyping and shipping performant code: Span<T>, memory-safe low-level patterns, and NativeAOT allow shipping native-like binaries with a smaller migration surface than rewriting in Rust/C++.
18. - Node remains the best choice for browser-based visualization and developer tooling; keeping visualization in Node preserves a clean separation of concerns.
19. 
20. Alternatives considered
21. 
22. 1) Python-first core (original ADR)
23.   - Pros: rapid prototyping, rich scientific ecosystem.
24.   - Cons: harder to reach control-loop performance without native extensions; increased friction shipping native-like binaries.
25. 
26. 2) Rust-first core with bindings for Node
27.   - Pros: best raw performance and safety.
28.   - Cons: longer ramp, steeper contributor bar for domain engineers; higher early development cost.
29. 
30. 3) Mixed-language microservices
31.   - Pros: pick-best-tool-per-subsystem.
32.   - Cons: early complexity and deployment/observability overhead; harder to ensure deterministic single-threaded sim loop.
33. 
34. Trade-offs
35. 
36. - Choosing C# prioritizes performance and strong typing while keeping developer ergonomics close to higher-level languages. We accept slightly longer initial implementation time compared to Python but expect fewer later rewrites.
37. - Native interop is an available escape hatch, but should be used only for proven hotspots.
38. 
39. Consequences
40. 
41. - Immediate: implement simulator core in C# under src/SimCore/ as a .NET solution with projects for core, models, sensors, and IO. Public API (Simulator, Frame) will be idiomatic C# and versioned.
42. - IPC: keep protobuf (strongly recommended) for long-term stability/versioning; JSON over websocket remains acceptable for early developer tooling and debugging. Schemas must be versioned.
43. - Migration: existing Python prototypes (if any) will be archived for reference; porting tests and validation harnesses to .NET is required to keep acceptance criteria.
44. 
45. Migration notes
46. 
47. - Port unit tests and deterministic golden-file fixtures into .NET test projects (xUnit/NUnit). Keep identical numeric tolerances and test vectors.
48. - Maintain protobuf schemas for IPC so visualization and tools can remain language-agnostic during migration.
49. - Start with a compact, well-tested C# Simulator API surface (Simulator.Step(double dt) -> Frame) and a simple websocket/JSON debug adapter for tooling; migrate to protobuf/gRPC for production IPC once stable.
50. 
51. Next steps
52. 
53. 1. Update docs/simulator-architecture.md to reflect C# primary core and module layout (this ADR overrides the prior Python recommendation).  
54. 2. Implement minimal C# simulator scaffold (WorldState, Simulator, simple Euler integrator, reference helicopter model) as src/SimCore/SimCore.sln solution.  
55. 3. Provide Node visualization that consumes Frame messages over websocket (JSON for MVP, protobuf recommended for prod).  
56. 
57. Tasks and owners (short-term)
58. 
59. - Rusty: Physics core and integrators in C# (SimCore.Physics project) — Estimate: Medium (M)
60. - Linus: WorldState, Entity models, deterministic serialization and tests in C# (SimCore.State project) — Estimate: Small (S)
61. - Both: IPC/protobuf schema and Node visualization adapter — Estimate: Medium (M)
62. 
63. Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
