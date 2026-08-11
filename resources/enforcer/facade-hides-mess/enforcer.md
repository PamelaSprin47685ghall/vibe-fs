# facade-hides-mess — Enforcer

## Definition
A facade hides mess when a clean-looking entry point is placed over duplicated ownership, tangled dependencies, or broken boundaries without changing the structure beneath it.

## Governing Principle
Abstraction is compression only when it removes knowledge from callers because the hidden details have one coherent owner. A facade over disorder does the opposite: it hides evidence while preserving every underlying coupling. The public surface becomes simpler, but the system itself does not; complexity is merely moved out of sight, where it can grow without pressure to become coherent.

## Trigger When
Trigger when a new wrapper/facade primarily forwards into an unhealthy subsystem and is presented as the architectural fix even though ownership and dependency violations remain unchanged.

## Do Not Trigger When
- The facade genuinely defines and enforces a stable subsystem boundary whose internals already have coherent ownership.
- A narrow adapter translates a foreign API at the edge without claiming to have repaired the interior.
- The wrapper is a temporary spike labeled as such, not shipped as the architecture.
- Callers already faced a coherent module and the facade only names that existing contract.

## Distinguish From
`translator-layer-bloat` concerns pure forwarding ceremony. `dirty-hack` concerns local workaround. This rule is architectural concealment: a neat front door over unresolved internal structure. Tie-break: if the wrapper is offered as the fix while the dependency/ownership graph beneath is unchanged, this rule owns the case.

## Decision Procedure
Ignore the facade and inspect the dependency/ownership graph beneath it. If the same violations still exist and the wrapper enforces no new invariant, it is concealment rather than abstraction.

## Examples
- positive: a new `OrderService` facade forwards into tangled modules that still import each other’s internals; the PR calls this “the new architecture.”
- near-miss: a facade over a module that already has one owner and acyclic internals, exposing a real contract.
- counterexample: repair ownership and dependencies first; keep a facade only if it represents that repaired boundary.

## Nudge
A clean entrance does not make a tangled house well designed. Repair ownership and dependency structure first; keep a facade only if it represents a real boundary afterward.
