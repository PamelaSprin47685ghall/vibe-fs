# cyclic-dependency — Lesson

## Opinion

### Definition

A dependency cycle exists when components require one another’s definitions, initialization, or services to become meaningful, so there is no direction in which the system can be understood or constructed. The root-cause is that the fact both sides need has no independent owner, so each module must be defined in terms of the other.

### Governing Principle

A dependency edge is an arrow of knowledge: A → B means A is defined partly in terms of B. A cycle therefore says each participant must already exist for the other to be defined. Runtimes may break the physical loop with lazy initialization or service locators, but the conceptual loop remains and appears later as ordering constraints, partial states, and tests that need the whole graph.

### Trigger When

Trigger when modules, packages, projects, or services form a directed cycle, require mutual initialization, or use indirection solely to hide such a cycle.

### Do Not Trigger When

- Reciprocal domain communication is mediated through an independent protocol and compile-time/ownership dependencies remain acyclic.
- Two peers exchange messages through a third-party bus, queue, or contract neither owns, with no import cycle.
- A type and its tests refer to each other in the usual subject/fixture direction; that is not a production ownership cycle.
- Lazy loading exists for performance of an already acyclic graph, not to conceal mutual definition.

### Distinguish From

`boundary-collapse` concerns excessive knowledge across contexts. `implicit-control-flow` concerns hidden ordering. This rule concerns the dependency graph having no unidirectional foundation. Tie-break: if A and B must each exist for the other to be defined, this rule owns the case even when runtime indirection hides the edge.

### Decision Procedure

Draw the semantic dependency graph. Find the smallest cycle and ask which fact or abstraction is owned by neither side cleanly. Extract that fact/protocol so dependencies point one way.

### Examples

- positive: package A imports package B for a type, and B imports A to construct that type; neither can be understood first.
- near-miss: two services send events through a shared protocol module both depend on, with no A↔B compile-time cycle.
- counterexample: extract the shared fact into an owner both sides may depend on, restoring a one-way graph.

## Advice

### What To Do Now

Break the smallest semantic cycle by extracting the shared fact, protocol, or policy into an owner both sides may depend on without depending on each other. That extracted concept is who owns the shared fact; neither cyclic peer is who owns the foundation the other needs.

### Why This Matters

Cycles destroy locality. To understand A you must understand B, but to understand B you must already understand A. Construction inherits the same paradox through lazy initialization, mutable registries, or runtime lookups. The code can execute, yet no component has an independent definition.

### Repair Strategy

Draw dependency arrows by meaning, not file references. Decide which direction reflects authority. Move shared abstractions toward the stable foundation or introduce message/data contracts that let peers communicate without importing each other.

### Decision Branches

- If both sides need the same fact, extract that fact into a third owner and point both arrows at it.
- If one side is clearly the authority, invert the other dependency so knowledge flows one way.
- If runtime indirection only hides a remaining semantic cycle, treat it as unfixed; remove the mutual ownership.

### Common Wrong Fixes

- Do not hide the cycle behind dependency injection, a global service locator, dynamic import, or callback registry.
- Do not declare the cycle “runtime-only” while construction still requires both implementations.
- Do not merge the two modules into one god unit merely to erase the edge on paper.
- Do not add a third wrapper that still imports both sides and is imported by both.

### Verification

The module/package graph should be acyclic, initialization should have a clear order, and either side should be testable against a contract without constructing the other’s implementation. The invariant is that lower concepts can be understood and constructed before higher ones.

### Done When

Dependencies form a one-way explanation of the system: lower concepts can be understood before higher ones, and no component requires its dependent in order to define itself.

### Nudge

A cycle usually marks a missing third concept. Name the fact both sides need, give it an owner, and restore a dependency graph that can be understood from foundations upward.
