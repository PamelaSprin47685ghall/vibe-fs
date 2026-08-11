# random-source-in-logic — Enforcer

## Definition
Domain policy draws randomness internally, so the same inputs cannot replay to the same decisions.

## Trigger When
Domain logic generates randomness internally and cannot be replayed from explicit input.

## Do Not Trigger When
Do not fire when randomness lives only in an adapter (UI jitter, crypto nonce generation) outside pure domain decision functions.

## Distinguish From
time-source-in-logic hides the clock; this tip hides entropy. Both destroy replay.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Randomness is hidden inside policy. Inject a seed or random source and preserve replayability.

## Examples
### Positive
Domain logic generates randomness internally and cannot be replayed from explicit input.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when randomness lives only in an adapter (UI jitter, crypto nonce generation) outside pure domain decision functions.
