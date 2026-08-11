# time-source-in-logic — Main

## What To Do Now
Move system-clock reads to the shell and pass an explicit instant or clock value into the domain decision that needs temporal context.

## Why This Matters
Hidden time makes identical visible inputs non-identical in reality. That breaks deterministic tests, event replay, incident reconstruction, and explanations of why a deadline or eligibility decision differed between runs.

## Repair Strategy
Read the clock once at the owning boundary, normalize zone/precision there, and pass the instant through pure policy. Use a clock port only when multiple observations during one operation are semantically necessary.

## Wrong Fixes
Do not globally mock `now()` in tests while production logic still reaches ambient time. A controllable global remains a hidden dependency.

## Verification
Run the core repeatedly with the same supplied instant and require identical outcomes; vary the instant explicitly to test the temporal rule’s boundaries.

## Done When
Every time-sensitive decision can be replayed from recorded inputs because the moment it used is visible data rather than an ambient observation.
