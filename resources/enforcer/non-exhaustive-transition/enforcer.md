# non-exhaustive-transition — Enforcer

## Definition
A transition is non-exhaustive when a finite state/event relation contains cells whose semantics are supplied by `default`, wildcard, silent ignore, generic error, or fallthrough rather than by an explicit domain decision.

The root cause is **unowned cells in the transition table**. A closed state machine exists, but some combinations have never been classified as legal successor, idempotent no-op, explicit rejection, or impossible input. Control-flow convenience fills the gap.

## Governing Principle
A finite state machine is a relation, not a collection of convenient handlers.

For every reachable `state × event`, the system should know what that pair means. “We have a default” does not answer that question. It merely says several semantically unreviewed pairs inherited the same behavior from syntax.

Wildcards are especially dangerous under evolution. Add a new event and the compiler stays green; the new event quietly receives yesterday's default semantics. The system has accepted a new domain case without anyone deciding what it means.

Exhaustiveness is valuable not because every case deserves bespoke code, but because **equivalence itself should be deliberate**. Several pairs may legally map to the same `Ignored` result; name that law and prove those pairs belong together.

## Trigger When
Trigger when:

- a closed union/enum of states or events is handled with wildcard/default behavior;
- unknown combinations silently return the current state;
- partial maps omit pairs and a generic fallback decides them;
- adding a new state/event does not force transition policy to be reconsidered;
- tests cover only “happy” transitions while unmentioned pairs inherit behavior;
- illegal transitions are logged and ignored rather than represented as a typed rejection with a reason.

## Do Not Trigger When
- The input space is intentionally open-world and the protocol defines stable semantics for unknown future values, such as “preserve unknown extension frames.”
- The function is open plugin dispatch rather than a finite domain transition relation.
- A wildcard covers a set already made impossible by prior typed narrowing, and that impossibility is mechanically enforced.
- A named domain law intentionally maps a closed, reviewed set of events to the same result, and adding a new case still forces the set to be revisited.

## Distinguish From
`catch-all-swallows-future` is the broader defect of catch-alls hiding future variants anywhere. `illegal-state-representable` concerns bad values inside a state. `phase-flag-accumulation` concerns a lifecycle represented by flag soup.

Tie-break: if the missing decision is specifically a cell in a finite transition relation, use this rule. If a wildcard generally erases future variant obligations outside state-machine semantics, use `catch-all-swallows-future`.

## Decision Procedure
Write the table.

Rows are reachable states. Columns are events/inputs. Every cell must be one of:

- legal successor;
- explicit idempotent/no-op law;
- typed rejection;
- mechanically unreachable.

Any cell described only as “falls through default” is unfinished domain design.

Then add a fake new event in a compile/test exercise. If it silently inherits behavior rather than creating a visible decision obligation, exhaustiveness is still weak.

## Examples
- positive: `switch event { Start -> ...; Stop -> ...; _ -> state }` in a closed lifecycle silently ignores `Cancel` and every future event.
- positive: a transition dictionary returns `None`, and callers uniformly “keep current state” without deciding whether the missing pair is illegal or idempotent.
- near-miss: a versioned extension protocol deliberately stores unknown vendor frames and specifies “unknown frames are preserved but never executed.” The world is intentionally open.
- counterexample: exhaustive matching returns `Next state | NoOp reason | IllegalTransition {state; event}` for every closed pair.

## Nudge
A wildcard is not a domain decision. It is where undecided cases go to become invisible.

If the state and event sets are finite, make the relation finite and explicit too.
