# missing-regression-test — Lesson

## Opinion

### Definition

A missing regression test means the team paid to discover a real defect, fixed the code, and then failed to preserve the **new knowledge about reachable bad behavior**.

A bug report is not merely a request to change lines. It is evidence that the system's state space contains a counterexample nobody expected or nobody had encoded. The implementation fix removes today's symptom. A regression test turns the discovery into repository memory.

### Governing Principle

The important distinction from `failure-path-untested` is provenance: this rule starts from **a known concrete defect**. `failure-path-untested` can fire before any incident because a failure policy has never been exercised. A known bug deserves a test even if its path was nominally “covered” before — because coverage clearly failed to distinguish the actual defect.

### Trigger When

Fire this rule when:

- a concrete production/test/user-reported defect is fixed but no executable scenario reproduces it;
- the new test exercises the repaired code but would also have passed under the old buggy behavior;
- the test asserts an implementation detail introduced by the fix instead of the externally meaningful failure;
- the bug depended on a boundary case (timezone, stale version, cancellation race, malformed input, duplicate delivery, migration state) and that exact boundary case remains absent from the suite;
- an incident postmortem describes the failure, but no test/property/canary prevents the same mechanism from returning;
- a one-off manual reproduction was used to verify the fix and then discarded.

### Do Not Trigger When

Do not fire when an existing test already caught the defect and remains in the suite. That test is already the regression memory. Do not demand a regression test for documentation-only errors or operational mistakes outside the product's behavioral contract unless product behavior is changed to prevent recurrence.

### Distinguish From


### Decision Procedure

A strong regression test has three properties:

1. it reproduces the original failure through the owning behavioral boundary;
2. it fails against the old mechanism for the same material reason users observed;
3. it stays meaningful after internal refactors because it protects the promise, not the patch shape.

The best regression is often smaller than the original incident. Strip incidental environment until only the causal ingredients remain, but do not simplify away the condition that made the bug possible.

For concurrency/nondeterministic incidents, preserve the causal schedule deterministically with barriers/fake clocks/controlled ordering instead of writing a flaky “run 1000 times” test and hoping the race appears.

For property bugs, a found minimal counterexample may belong both as a fixed example and as evidence strengthening the general generator/property.

A decisive check is to temporarily restore or simulate the old defect. If the new test remains green, it is not regression memory; it is ceremony added after the fact.

### Examples


## Advice

### What To Do Now

Turn the defect into executable memory.

Reproduce the smallest scenario that still contains the causal ingredients of the original failure, enter through the owning behavioral boundary, and assert the caller-visible wrong/right outcome.

### Why This Matters

Do not start from the patch. Start from the incident:

```text
Given the conditions that existed,
what should have happened,
what actually happened,
and what observable would distinguish the two?
```

Preserve the cause, not the noise. A production incident may involve large logs, many services, timing, unrelated retries, and historical residue. Reduce those only after identifying which facts were load-bearing. A tiny test that no longer reproduces the cause is worse than a larger faithful one.

### Repair Strategy

Then prove the test would have caught the old behavior. If production is already fixed, temporarily revert/mutate the relevant mechanism or construct the old outcome in a controlled branch. The regression is not established until the test demonstrably turns red under the defect.

For boundary defects, keep the regression at the boundary that failed. For serializer/protocol bugs, make the incompatible bytes/identity part of the fixture. For recovery bugs, crash/fault at the real transition. For concurrency bugs, encode the causal ordering with barriers. For timezone/calendar bugs, freeze the exact instant/zone. For duplicate-delivery bugs, replay the same identity twice.

If the defect revealed a broader law, add the regression **and** strengthen the property/contract. The concrete counterexample remains valuable because it records what humans actually paid to learn; the property protects the surrounding space.

### Decision Branches


### Common Wrong Fixes

Common fake repairs:

- add a test that calls the new helper introduced by the fix, so old code could never even run the test;
- assert a new field/type exists instead of the behavior users lost;
- snapshot the repaired internal structure rather than the bug's public consequence;
- create a test with the same input shape but omit the stale/cancelled/duplicate/versioned condition that actually triggered the defect;
- rely on a comment or issue link as institutional memory;
- keep only a manual repro command in a ticket;
- write a stress loop that occasionally hits the old race instead of controlling the schedule deterministically.

### Verification

Verification is straightforward: old defect red, repaired behavior green. Also perform a semantics-preserving refactor to ensure the test does not depend on the exact patch decomposition.

### Done When

You are done when the repository can no longer recreate the same material failure without a test turning red before delivery.

> A fix repairs today's code. A regression test repairs the project's memory.

### Nudge

> A bug that changed code but did not change the repository's executable knowledge is only waiting to become expensive twice.
