# non-exhaustive-transition — Enforcer

## Definition
A state transition is non-exhaustive when some state/event combinations fall through a generic branch, are silently ignored, or are accepted without an explicit decision that they are legal or illegal.

## Governing Principle
A finite state machine is a relation, not a bag of handlers. For every reachable state and input, the domain either permits a defined successor or forbids the transition. Leaving cells unspecified delegates semantics to control-flow accidents such as default branches. Exhaustiveness turns the transition table into a complete specification and makes future states/events create visible obligations.

## Trigger When
Trigger when finite state/event types are handled by wildcard/default logic, partial maps, ignored cases, or generic success/error paths that obscure legality.

## Do Not Trigger When
Do not trigger when the state/input domain is intentionally open and the protocol defines stable semantics for unknown cases.

## Distinguish From
illegal-state-representable concerns invalid values within a state. catch-all-swallows-future concerns future variants generally. This rule concerns completeness of the transition relation itself.

## Decision Procedure
Construct the state × input table. Mark every cell as a legal next state/event or an explicit rejection. Any unclassified cell is a missing domain decision.

## Nudge
A finite transition system deserves a complete table. Enumerate every legal and illegal pair so no state change inherits semantics from a catch-all by accident.
