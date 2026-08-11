# cyclic-dependency — Enforcer

## Definition
A dependency cycle exists when components require one another’s definitions, initialization, or services to become meaningful, so there is no direction in which the system can be understood or constructed.

## Governing Principle
A dependency edge is an arrow of knowledge: A → B means A is defined partly in terms of B. A cycle therefore says each participant must already exist for the other to be defined. Runtimes may break the physical loop with lazy initialization or service locators, but the conceptual loop remains and appears later as ordering constraints, partial states, and tests that need the whole graph.

## Trigger When
Trigger when modules, packages, projects, or services form a directed cycle, require mutual initialization, or use indirection solely to hide such a cycle.

## Do Not Trigger When
- Reciprocal domain communication is mediated through an independent protocol and compile-time/ownership dependencies remain acyclic.
- Two peers exchange messages through a third-party bus, queue, or contract neither owns, with no import cycle.
- A type and its tests refer to each other in the usual subject/fixture direction; that is not a production ownership cycle.
- Lazy loading exists for performance of an already acyclic graph, not to conceal mutual definition.

## Distinguish From
`boundary-collapse` concerns excessive knowledge across contexts. `implicit-control-flow` concerns hidden ordering. This rule concerns the dependency graph having no unidirectional foundation. Tie-break: if A and B must each exist for the other to be defined, this rule owns the case even when runtime indirection hides the edge.

## Decision Procedure
Draw the semantic dependency graph. Find the smallest cycle and ask which fact or abstraction is owned by neither side cleanly. Extract that fact/protocol so dependencies point one way.

## Examples
- positive: package A imports package B for a type, and B imports A to construct that type; neither can be understood first.
- near-miss: two services send events through a shared protocol module both depend on, with no A↔B compile-time cycle.
- counterexample: extract the shared fact into an owner both sides may depend on, restoring a one-way graph.

## Nudge
A cycle usually marks a missing third concept. Name the fact both sides need, give it an owner, and restore a dependency graph that can be understood from foundations upward.
