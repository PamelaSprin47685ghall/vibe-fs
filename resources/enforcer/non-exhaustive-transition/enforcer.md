# non-exhaustive-transition — Enforcer

## Definition
A state transition is non-exhaustive when some state/event combinations fall through a generic branch, are silently ignored, or are accepted without an explicit decision that they are legal or illegal. The root-cause is that unspecified cells of a finite state×input relation inherit semantics from a wildcard, so legality is an accident of control flow rather than a domain decision.

## Governing Principle
A finite state machine is a relation, not a bag of handlers. For every reachable state and input, the domain either permits a defined successor or forbids the transition. Leaving cells unspecified delegates semantics to control-flow accidents such as default branches. Exhaustiveness turns the transition table into a complete specification and makes future states/events create visible obligations.

## Trigger When
Trigger when finite state/event types are handled by wildcard/default logic, partial maps, ignored cases, or generic success/error paths that obscure legality.

## Do Not Trigger When
- The state/input domain is intentionally open and the protocol defines stable semantics for unknown cases.
- The handler is not a finite transition relation (for example open plugin dispatch).
- Every reachable pair is already named as a successor or an explicit rejection.
- The default branch is itself a named domain law for a closed leftover set, and adding a case still forces that leftover to be re-decided.

## Distinguish From
`illegal-state-representable` concerns invalid values within a state. `catch-all-swallows-future` concerns future variants generally. Tie-break: if the transition relation itself has unspecified cells, this rule; if a value inside a state can be illegal, `illegal-state-representable`; if a wildcard hides future cases outside a state machine, `catch-all-swallows-future`.

## Decision Procedure
Construct the state × input table. Mark every cell as a legal next state/event or an explicit rejection. Any unclassified cell is a missing domain decision.

## Examples
- positive: `switch (event)` in `Open` has a `default: return state` that silently ignores `Ship` and future events.
- near-miss: A versioned wire protocol defines "unknown frame → skip with log" as a stable open-world rule.
- counterexample: Exhaustive matching names every pair as a successor or a typed `IllegalTransition`.

## Nudge
A finite transition system deserves a complete table. Enumerate every legal and illegal pair so no state change inherits semantics from a catch-all by accident.
