# time-source-in-logic — Main

## What To Do Now
Move system-clock reads to the shell and pass an explicit instant or clock value into the domain decision that needs temporal context. The shell/adapter is who owns clock observation; domain policy owns only interpretation of a supplied instant.

## Why This Matters
Hidden time makes identical visible inputs non-identical in reality. That breaks deterministic tests, event replay, incident reconstruction, and explanations of why a deadline or eligibility decision differed between runs.

## Repair Strategy
Read the clock once at the owning boundary, normalize zone/precision there, and pass the instant through pure policy. Use a clock port only when multiple observations during one operation are semantically necessary.

## Decision Branches
If the decision needs a temporal fact, add it to the function’s inputs and keep the core deterministic.
If multiple observations during one operation are semantically required, inject a clock port rather than reading the ambient clock inside policy.

## Common Wrong Fixes
- Globally mock `now()` in tests while production logic still reaches ambient time.
- Pass a clock into some call sites and leave others reading the system clock.
- Snapshot wall time into a hidden module singleton and call the core “pure.”

## Verification
Invariant: identical supplied instants must yield identical core outcomes. Run the core repeatedly with the same instant; vary the instant explicitly to test temporal rule boundaries.

## Done When
Every time-sensitive decision can be replayed from recorded inputs because the moment it used is visible data rather than an ambient observation.
