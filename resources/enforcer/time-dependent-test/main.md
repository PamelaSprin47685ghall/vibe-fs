# time-dependent-test — Main

## What To Do Now
Inject a controllable clock or explicit instant/time zone into the scenario and replace real waits with deterministic clock advancement or causal synchronization. The test fixture is who owns the temporal facts; the host clock must not supply the premises of the verdict.

## Why This Matters
A test that depends on the current clock has moving premises. It may cross midnight, DST, timeout thresholds, or scheduling delays differently on every machine. Failures then mix domain defects with facts about when and where CI happened to run.

## Repair Strategy
Fix instants and zones in test data, use a manual clock for deadlines and expiration, and reserve real-time integration only for proving the adapter that reads the system clock.

## Decision Branches
If the scenario needs a temporal fact (instant, duration, zone, deadline), supply it explicitly via a fake/manual clock.
If the test’s purpose is only to prove clock wiring, keep a narrow non-semantic smoke and do not use it as a domain verdict.

## Common Wrong Fixes
- Widen timing tolerances until the test usually passes.
- Sleep longer to outrun scheduler noise while still using wall time as the premise.
- Globally mock `Date.now` in one test file while other tests still read ambient time.

## Verification
Invariant: the test’s meaning and verdict must be independent of the host’s real clock. Run it at arbitrary real times and zones; results must stay identical because all relevant temporal facts are explicit.

## Done When
Temporal behavior is tested as data and policy, while wall-clock timing no longer participates accidentally in the test’s premises.
