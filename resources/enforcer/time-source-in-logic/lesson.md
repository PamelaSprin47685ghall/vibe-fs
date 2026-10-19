# time-source-in-logic — Lesson

## Opinion

### Definition

Time is hidden in logic when domain policy reads the current clock internally instead of receiving the relevant instant or clock as an explicit dependency. The root-cause is that ambient time is an undeclared policy input, so identical visible arguments are not identical decisions and replay from recorded data is impossible.

### Governing Principle

The present moment is an input, not a universal constant. A decision that calls `now()` internally changes meaning between two otherwise identical invocations, making replay and causal explanation impossible from visible data alone. Moving the clock to the boundary restores referential clarity: the shell observes time; the policy reasons about a supplied temporal fact.

### Trigger When

Trigger when core rules directly read system time to decide expiry, eligibility, ordering, deadlines, windows, age, or lifecycle outcomes.

### Do Not Trigger When

- Clock reads are confined to adapters or orchestration that pass an explicit instant into deterministic domain functions.
- Display formatting of a supplied timestamp with no policy branching on ambient time.
- One-shot logging of wall time at the shell that does not affect decisions.
- Tests that inject a clock into production-shaped ports rather than hiding `now()` inside policy.

### Distinguish From

`random-source-in-logic` hides entropy. `time-dependent-test` is the verification symptom. `impure-core` is the broader principle. Tie-break: if ambient time is an undeclared policy input in production logic, use this rule; if only the test verdict depends on the real clock, use `time-dependent-test`.

### Decision Procedure

Rewrite the decision signature conceptually with `now` among its inputs. If doing so makes the real dependency clearer and enables replay, move clock observation outward.

### Examples

- positive: eligibility code calls `Date.now()` inside the domain to decide whether a window is open.
- near-miss: the HTTP adapter reads the clock once and passes `asOf` into a pure function.
- counterexample: a test that sleeps until midnight to assert expiry is `time-dependent-test`.

## Advice

### What To Do Now

Move system-clock reads to the shell and pass an explicit instant or clock value into the domain decision that needs temporal context. The shell/adapter is who owns clock observation; domain policy owns only interpretation of a supplied instant.

### Why This Matters

Hidden time makes identical visible inputs non-identical in reality. That breaks deterministic tests, event replay, incident reconstruction, and explanations of why a deadline or eligibility decision differed between runs.

### Repair Strategy

Read the clock once at the owning boundary, normalize zone/precision there, and pass the instant through pure policy. Use a clock port only when multiple observations during one operation are semantically necessary.

### Decision Branches

If the decision needs a temporal fact, add it to the function’s inputs and keep the core deterministic.
If multiple observations during one operation are semantically required, inject a clock port rather than reading the ambient clock inside policy.

### Common Wrong Fixes

- Globally mock `now()` in tests while production logic still reaches ambient time.
- Pass a clock into some call sites and leave others reading the system clock.
- Snapshot wall time into a hidden module singleton and call the core “pure.”

### Verification

Invariant: identical supplied instants must yield identical core outcomes. Run the core repeatedly with the same instant; vary the instant explicitly to test temporal rule boundaries.

### Done When

Every time-sensitive decision can be replayed from recorded inputs because the moment it used is visible data rather than an ambient observation.

### Nudge

The clock reports a fact; policy interprets it. Read time at the boundary and pass the relevant instant inward so identical inputs produce identical decisions.
