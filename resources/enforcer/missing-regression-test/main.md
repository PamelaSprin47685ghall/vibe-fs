# missing-regression-test — Main

Turn the defect into executable memory.

Reproduce the smallest scenario that still contains the causal ingredients of the original failure, enter through the owning behavioral boundary, and assert the caller-visible wrong/right outcome.

Do not start from the patch. Start from the incident:

```text
Given the conditions that existed,
what should have happened,
what actually happened,
and what observable would distinguish the two?
```

Then prove the test would have caught the old behavior. If production is already fixed, temporarily revert/mutate the relevant mechanism or construct the old outcome in a controlled branch. The regression is not established until the test demonstrably turns red under the defect.

Preserve the cause, not the noise. A production incident may involve large logs, many services, timing, unrelated retries, and historical residue. Reduce those only after identifying which facts were load-bearing. A tiny test that no longer reproduces the cause is worse than a larger faithful one.

Common fake repairs:

- add a test that calls the new helper introduced by the fix, so old code could never even run the test;
- assert a new field/type exists instead of the behavior users lost;
- snapshot the repaired internal structure rather than the bug's public consequence;
- create a test with the same input shape but omit the stale/cancelled/duplicate/versioned condition that actually triggered the defect;
- rely on a comment or issue link as institutional memory;
- keep only a manual repro command in a ticket;
- write a stress loop that occasionally hits the old race instead of controlling the schedule deterministically.

For boundary defects, keep the regression at the boundary that failed. For serializer/protocol bugs, make the incompatible bytes/identity part of the fixture. For recovery bugs, crash/fault at the real transition. For concurrency bugs, encode the causal ordering with barriers. For timezone/calendar bugs, freeze the exact instant/zone. For duplicate-delivery bugs, replay the same identity twice.

If the defect revealed a broader law, add the regression **and** strengthen the property/contract. The concrete counterexample remains valuable because it records what humans actually paid to learn; the property protects the surrounding space.

Verification is straightforward: old defect red, repaired behavior green. Also perform a semantics-preserving refactor to ensure the test does not depend on the exact patch decomposition.

You are done when the repository can no longer recreate the same material failure without a test turning red before delivery.

> A fix repairs today's code. A regression test repairs the project's memory.