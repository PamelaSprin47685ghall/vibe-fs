# cyclic-dependency — Main

## What To Do Now
Break the smallest semantic cycle by extracting the shared fact, protocol, or policy into an owner both sides may depend on without depending on each other. That extracted concept is who owns the shared fact; neither cyclic peer is who owns the foundation the other needs.

## Why This Matters
Cycles destroy locality. To understand A you must understand B, but to understand B you must already understand A. Construction inherits the same paradox through lazy initialization, mutable registries, or runtime lookups. The code can execute, yet no component has an independent definition.

## Repair Strategy
Draw dependency arrows by meaning, not file references. Decide which direction reflects authority. Move shared abstractions toward the stable foundation or introduce message/data contracts that let peers communicate without importing each other.

## Decision Branches
- If both sides need the same fact, extract that fact into a third owner and point both arrows at it.
- If one side is clearly the authority, invert the other dependency so knowledge flows one way.
- If runtime indirection only hides a remaining semantic cycle, treat it as unfixed; remove the mutual ownership.

## Common Wrong Fixes
- Do not hide the cycle behind dependency injection, a global service locator, dynamic import, or callback registry.
- Do not declare the cycle “runtime-only” while construction still requires both implementations.
- Do not merge the two modules into one god unit merely to erase the edge on paper.
- Do not add a third wrapper that still imports both sides and is imported by both.

## Verification
The module/package graph should be acyclic, initialization should have a clear order, and either side should be testable against a contract without constructing the other’s implementation. The invariant is that lower concepts can be understood and constructed before higher ones.

## Done When
Dependencies form a one-way explanation of the system: lower concepts can be understood before higher ones, and no component requires its dependent in order to define itself.
