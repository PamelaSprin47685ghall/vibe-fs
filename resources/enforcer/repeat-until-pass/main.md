# repeat-until-pass — Main

## What To Do Now
Stop looping until green. Capture the failure, remove the nondeterminism (time, order, shared state, network), and make one run sufficient.

## Repair Strategy
Quarantine the flaky command. Fix root cause: inject clocks, isolate state, wait on signals, bound concurrency. Delete retry-until-pass scripts and CI rerun crutches used as proof.

## Decision Branches
If infrastructure flakes are external, gate on a health check with capped retries and still fail the job when exhausted. Never hide product nondeterminism behind reruns.

## Wrong Fixes
CI "rerun failed jobs" as the merge strategy. Local shells looping pytest until exit 0. Calling a flaky suite stable because the third try passed.

## Verification
A single clean run passes repeatedly; deliberate fault injection fails once without needing loops.

## Done When
Verification is a single deterministic run; repeat-until-pass is gone from scripts and habits.

## Scope and Authority
Tests and verification commands. Not user-facing product retry UX with idempotency.
