Title: Initial project scaffold and language defaults

Date: 2026-06-14T12:56:10Z

Status: proposed

Context:
- We need a minimal, language-agnostic scaffold for local development to onboard quickly.

Decision:
- Scaffold both minimal Node and Python artifacts (package.json + pyproject.toml), plus simple example code and tests.

Consequences / Trade-offs:
- Pros: Keeps options open for backend language; lowers friction for contributors preferring JS/Node or Python.
- Cons: Slight repository clutter and maintenance cost; CI must be configured by team to pick a primary language.
- If the team later chooses one language, we should remove the other and update CI and docs.

Rationale:
- Starting with both covers most use cases and avoids blocking work while the team decides.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
