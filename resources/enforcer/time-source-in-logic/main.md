# time-source-in-logic — Main

## What To Do Now
Pass `now` or a clock port into domain functions. Keep `UtcNow` at the edge. Persist decision time with the fact when audit matters.

## Repair Strategy
Find ambient clock reads in domain code. Thread explicit instants. Update callers and tests to supply time.

## Decision Branches
If multiple reads during one decision must match, take one instant at the boundary and reuse it. If monotonic deadlines differ from wall time, name both ports.

## Wrong Fixes
Calling DateTime.UtcNow deep in pure folds. Seeding "random" from the clock inside domain services. Tests that cannot freeze expiration logic.

## Verification
Same inputs and same provided time yield identical decisions; expiration tests advance a fake clock.

## Done When
Domain logic receives time explicitly; no hidden ambient clock remains in policy code.

## Scope and Authority
Domain and pure application decisions. Edge adapters may read OS time.
