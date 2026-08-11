# partial-write-assumption — Enforcer

## Definition
A partial-write assumption exists when recovery invents an intermediate commit state that the storage/effect contract does not expose, then writes logic to handle that imagined state.

## Governing Principle
Failure models must come from the boundary that owns atomicity. If storage defines outcomes as committed, not committed, or unknown, application code cannot gain correctness by imagining “half committed” states unsupported by that contract. Extra states enlarge recovery logic without adding evidence; they often lead to destructive repair of data that was actually valid.

## Trigger When
Trigger when code reasons about speculative partial appends/writes/effects despite the underlying API specifying atomic commit or a different explicit outcome model.

## Do Not Trigger When
Do not trigger when the storage contract genuinely permits torn/partial records and exposes enough durable structure to detect and recover them safely.

## Distinguish From
truncation-skips-damaged concerns real corruption in durable history. optimistic-retry-assumption concerns unknown external effects. This rule concerns manufacturing failure states not present in the owner’s contract.

## Decision Procedure
Read the exact storage/effect contract. Enumerate only outcomes it can produce and distinguish. Design recovery over those states, not over implementation folklore or imagined disk behavior.

## Nudge
Do not solve failures the boundary cannot produce. Let the storage contract define the state space, then recover only from outcomes you can actually observe and justify.
