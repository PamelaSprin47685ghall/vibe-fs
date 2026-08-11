# time-source-in-logic — Enforcer

## Definition
Time is hidden in logic when domain policy reads the current clock internally instead of receiving the relevant instant or clock as an explicit dependency. The root-cause is that ambient time is an undeclared policy input, so identical visible arguments are not identical decisions and replay from recorded data is impossible.

## Governing Principle
The present moment is an input, not a universal constant. A decision that calls `now()` internally changes meaning between two otherwise identical invocations, making replay and causal explanation impossible from visible data alone. Moving the clock to the boundary restores referential clarity: the shell observes time; the policy reasons about a supplied temporal fact.

## Trigger When
Trigger when core rules directly read system time to decide expiry, eligibility, ordering, deadlines, windows, age, or lifecycle outcomes.

## Do Not Trigger When
- Clock reads are confined to adapters or orchestration that pass an explicit instant into deterministic domain functions.
- Display formatting of a supplied timestamp with no policy branching on ambient time.
- One-shot logging of wall time at the shell that does not affect decisions.
- Tests that inject a clock into production-shaped ports rather than hiding `now()` inside policy.

## Distinguish From
`random-source-in-logic` hides entropy. `time-dependent-test` is the verification symptom. `impure-core` is the broader principle. Tie-break: if ambient time is an undeclared policy input in production logic, use this rule; if only the test verdict depends on the real clock, use `time-dependent-test`.

## Decision Procedure
Rewrite the decision signature conceptually with `now` among its inputs. If doing so makes the real dependency clearer and enables replay, move clock observation outward.

## Examples
- positive: eligibility code calls `Date.now()` inside the domain to decide whether a window is open.
- near-miss: the HTTP adapter reads the clock once and passes `asOf` into a pure function.
- counterexample: a test that sleeps until midnight to assert expiry is `time-dependent-test`.

## Nudge
The clock reports a fact; policy interprets it. Read time at the boundary and pass the relevant instant inward so identical inputs produce identical decisions.
