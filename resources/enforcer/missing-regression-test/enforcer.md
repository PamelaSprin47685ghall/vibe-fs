# missing-regression-test — Enforcer

A missing regression test means the team paid to discover a real defect, fixed the code, and then failed to preserve the **new knowledge about reachable bad behavior**.

A bug report is not merely a request to change lines. It is evidence that the system's state space contains a counterexample nobody expected or nobody had encoded. The implementation fix removes today's symptom. A regression test turns the discovery into repository memory.

Fire this rule when:

- a concrete production/test/user-reported defect is fixed but no executable scenario reproduces it;
- the new test exercises the repaired code but would also have passed under the old buggy behavior;
- the test asserts an implementation detail introduced by the fix instead of the externally meaningful failure;
- the bug depended on a boundary case (timezone, stale version, cancellation race, malformed input, duplicate delivery, migration state) and that exact boundary case remains absent from the suite;
- an incident postmortem describes the failure, but no test/property/canary prevents the same mechanism from returning;
- a one-off manual reproduction was used to verify the fix and then discarded.

Do not fire when an existing test already caught the defect and remains in the suite. That test is already the regression memory. Do not demand a regression test for documentation-only errors or operational mistakes outside the product's behavioral contract unless product behavior is changed to prevent recurrence.

The important distinction from `failure-path-untested` is provenance: this rule starts from **a known concrete defect**. `failure-path-untested` can fire before any incident because a failure policy has never been exercised. A known bug deserves a test even if its path was nominally “covered” before — because coverage clearly failed to distinguish the actual defect.

A strong regression test has three properties:

1. it reproduces the original failure through the owning behavioral boundary;
2. it fails against the old mechanism for the same material reason users observed;
3. it stays meaningful after internal refactors because it protects the promise, not the patch shape.

The best regression is often smaller than the original incident. Strip incidental environment until only the causal ingredients remain, but do not simplify away the condition that made the bug possible.

For concurrency/nondeterministic incidents, preserve the causal schedule deterministically with barriers/fake clocks/controlled ordering instead of writing a flaky “run 1000 times” test and hoping the race appears.

For property bugs, a found minimal counterexample may belong both as a fixed example and as evidence strengthening the general generator/property.

A decisive check is to temporarily restore or simulate the old defect. If the new test remains green, it is not regression memory; it is ceremony added after the fact.

> A bug that changed code but did not change the repository's executable knowledge is only waiting to become expensive twice.