# unbounded-fanout — Enforcer

## Definition
Tasks, requests, subprocesses, agents, or file operations are spawned without a finite concurrency bound.

## Trigger When
Tasks, requests, subprocesses, agents, or file operations are spawned without a finite concurrency bound.

## Do Not Trigger When
Do not fire when a documented small fixed fan-out is inherently bounded (e.g. map over a tiny constant set) or a semaphore already caps concurrency.

## Distinguish From
serial-when-parallel under-uses concurrency; resource-not-scoped misses lifetime; this tip lacks an upper bound on fan-out.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Concurrency is unbounded. Use a bounded map or semaphore and define cancellation behavior.

## Examples
### Positive
Tasks, requests, subprocesses, agents, or file operations are spawned without a finite concurrency bound.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when a documented small fixed fan-out is inherently bounded (e.g. map over a tiny constant set) or a semaphore already caps concurrency.
