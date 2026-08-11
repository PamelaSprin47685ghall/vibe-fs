# time-dependent-test — Main

## What To Do Now
Inject a controllable clock or explicit instant/time zone into the scenario and replace real waits with deterministic clock advancement or causal synchronization.

## Why This Matters
A test that depends on the current clock has moving premises. It may cross midnight, DST, timeout thresholds, or scheduling delays differently on every machine. Failures then mix domain defects with facts about when and where CI happened to run.

## Repair Strategy
Fix instants and zones in test data, use a manual clock for deadlines and expiration, and reserve real-time integration only for proving the adapter that reads the system clock.

## Wrong Fixes
Do not widen timing tolerances until the test usually passes. Larger windows reduce sensitivity while preserving the hidden temporal dependency.

## Verification
Run the test at arbitrary real times and zones; its meaning and verdict must remain unchanged because all relevant temporal facts are explicit.

## Done When
Temporal behavior is tested as data and policy, while wall-clock timing no longer participates accidentally in the test’s premises.
