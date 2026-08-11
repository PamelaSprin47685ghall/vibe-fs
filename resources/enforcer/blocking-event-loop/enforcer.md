# blocking-event-loop — Enforcer

## Definition
A synchronous wait, blocking process, filesystem call, sleep, or CPU-heavy loop runs on an event-loop or hook thread.

## Trigger When
A synchronous wait, blocking process, filesystem call, sleep, or CPU-heavy loop runs on an event-loop or hook thread.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when the observed pattern is intentional, documented, and verified at the owning contract.

## Distinguish From
Related tips that share vocabulary but different boundary.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Blocking work is running on the event loop. Move it behind an asynchronous boundary or worker.

## Examples
### Positive
A synchronous wait, blocking process, filesystem call, sleep, or CPU-heavy loop runs on an event-loop or hook thread.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
Blocking work is running on the event loop. Move it behind an asynchronous boundary or worker.
