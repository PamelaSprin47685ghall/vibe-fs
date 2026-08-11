# random-source-in-logic — Main

## What To Do Now
Thread an explicit Random/seed/entropy port into the decision function. Persist the seed with the decision when audit or replay matters.

## Repair Strategy
Replace internal RNG calls with a passed-in source. For tests, use a fixed seed. For production audit trails, record the seed or drawn values beside the outcome.

## Decision Branches
If non-replayable entropy is required (security tokens), keep it at the edge and pass the drawn value inward as data. If Monte Carlo logic is intentional, make the source a first-class input.

## Wrong Fixes
Seeding from the clock inside domain code. Global mutable RNG. Testing only the happy seed and calling it deterministic.

## Verification
Same inputs plus same seed reproduce identical outputs across runs.

## Done When
Domain functions that need entropy take it explicitly; replay and tests no longer depend on hidden RNG state.

## Scope and Authority
Pure and application domain decisions. Edge adapters may still call OS entropy.
