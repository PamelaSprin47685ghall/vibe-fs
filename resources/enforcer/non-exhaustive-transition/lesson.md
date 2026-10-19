# non-exhaustive-transition — Lesson

## Opinion

### Definition

A transition is non-exhaustive when a finite state/event relation contains cells whose semantics are supplied by `default`, wildcard, silent ignore, generic error, or fallthrough rather than by an explicit domain decision.

The root cause is **unowned cells in the transition table**. A closed state machine exists, but some combinations have never been classified as legal successor, idempotent no-op, explicit rejection, or impossible input. Control-flow convenience fills the gap.

### Governing Principle

A finite state machine is a relation, not a collection of convenient handlers.

For every reachable `state × event`, the system should know what that pair means. “We have a default” does not answer that question. It merely says several semantically unreviewed pairs inherited the same behavior from syntax.

Wildcards are especially dangerous under evolution. Add a new event and the compiler stays green; the new event quietly receives yesterday's default semantics. The system has accepted a new domain case without anyone deciding what it means.

Exhaustiveness is valuable not because every case deserves bespoke code, but because **equivalence itself should be deliberate**. Several pairs may legally map to the same `Ignored` result; name that law and prove those pairs belong together.

### Trigger When

Trigger when:

- a closed union/enum of states or events is handled with wildcard/default behavior;
- unknown combinations silently return the current state;
- partial maps omit pairs and a generic fallback decides them;
- adding a new state/event does not force transition policy to be reconsidered;
- tests cover only “happy” transitions while unmentioned pairs inherit behavior;
- illegal transitions are logged and ignored rather than represented as a typed rejection with a reason.

### Do Not Trigger When

- The input space is intentionally open-world and the protocol defines stable semantics for unknown future values, such as “preserve unknown extension frames.”
- The function is open plugin dispatch rather than a finite domain transition relation.
- A wildcard covers a set already made impossible by prior typed narrowing, and that impossibility is mechanically enforced.
- A named domain law intentionally maps a closed, reviewed set of events to the same result, and adding a new case still forces the set to be revisited.

### Distinguish From

`catch-all-swallows-future` is the broader defect of catch-alls hiding future variants anywhere. `illegal-state-representable` concerns bad values inside a state. `phase-flag-accumulation` concerns a lifecycle represented by flag soup.

Tie-break: if the missing decision is specifically a cell in a finite transition relation, use this rule. If a wildcard generally erases future variant obligations outside state-machine semantics, use `catch-all-swallows-future`.

### Decision Procedure

Write the table.

Rows are reachable states. Columns are events/inputs. Every cell must be one of:

- legal successor;
- explicit idempotent/no-op law;
- typed rejection;
- mechanically unreachable.

Any cell described only as “falls through default” is unfinished domain design.

Then add a fake new event in a compile/test exercise. If it silently inherits behavior rather than creating a visible decision obligation, exhaustiveness is still weak.

### Examples

- positive: `switch event { Start -> ...; Stop -> ...; _ -> state }` in a closed lifecycle silently ignores `Cancel` and every future event.
- positive: a transition dictionary returns `None`, and callers uniformly “keep current state” without deciding whether the missing pair is illegal or idempotent.
- near-miss: a versioned extension protocol deliberately stores unknown vendor frames and specifies “unknown frames are preserved but never executed.” The world is intentionally open.
- counterexample: exhaustive matching returns `Next state | NoOp reason | IllegalTransition {state; event}` for every closed pair.

## Advice

### What To Do Now

Make the finite transition relation explicit. Every reachable `state × event` pair must produce a named successor, an intentional idempotent/no-op result, a typed rejection, or be mechanically impossible.

The transition function owns those cells. A wildcard/default does not own semantics; it merely hides cells that nobody decided.

### Why This Matters

The most expensive state-machine bugs often do not come from a wrong branch. They come from a branch that was never designed.

A default such as “ignore everything else” feels defensive until a new event is added. Then the program compiles, tests may stay green, and the new event silently acquires old fallback behavior. A product/domain change has entered production without creating a policy review point.

Exhaustive matching converts ontology growth into work you can see. That is the point: when the world gains a new case, every finite policy depending on the old case set should be forced to answer whether the new case matters.

### Repair Strategy

1. Enumerate reachable states and event/input cases.
2. Build the state × event table.
3. Mark each cell as successor, no-op, rejection, or unreachable.
4. Encode that table with exhaustive matching or an equally total declarative relation.
5. Use typed `IllegalTransition`/`NoOp` rather than conflating rejection with silent retention of current state.
6. Add table/property tests that cover every finite pair.
7. Remove wildcard/default behavior that exists only to make the compiler quiet.

### Decision Branches

- Closed domain: require exhaustive policy.
- Intentionally extensible protocol: define the unknown-case law explicitly and keep that open-world adapter separate from closed domain transitions.
- Several pairs truly share semantics: group them explicitly; do not let a wildcard decide membership accidentally.
- A pair is impossible by construction: prove it with types/constructor rather than a comment saying “cannot happen.”

### Common Wrong Fixes

- Replace `_ -> state` with `_ -> Illegal` while still allowing future cases to fall there without review.
- Add logging to the default and call it explicit.
- Maintain a partial transition map and treat missing key as one universal meaning.
- Create a generated exhaustive table that nobody can inspect or relate to domain vocabulary; mechanical totality without readable policy is still poor design.
- Write comments listing ignored cases beside a wildcard instead of letting the case set enforce them.

### Verification

Enumerate all finite pairs in a property/table test and assert the exact successor/no-op/rejection category.

Then temporarily add a new event case. Build/test must fail or present an explicit unclassified cell until a semantic decision is made. If the new event silently receives an old fallback, the repair is incomplete.

Invariant: **every cell of a closed transition relation is an explicit domain decision.**

### Done When

The transition function reads as a complete policy, ontology growth creates visible review obligations, and no reachable pair derives meaning merely from falling through control flow.

### Nudge

A wildcard is not a domain decision. It is where undecided cases go to become invisible.

If the state and event sets are finite, make the relation finite and explicit too.
