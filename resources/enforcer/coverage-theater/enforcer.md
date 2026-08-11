# coverage-theater — Enforcer

## Definition
Coverage theater occurs when execution metrics are treated as evidence of correctness even though the tests assert little or nothing that distinguishes correct behavior from plausible defects.

## Governing Principle
Coverage measures reachability, not truth. A line can execute under a test that would remain green if its result were inverted, its identity corrupted, or its error swallowed. Verification begins only where a test states a proposition capable of being false. The value of an assertion is therefore not that it exists, but that a realistic defect would violate it.

## Trigger When
Trigger when tests increase line/branch/function coverage yet omit meaningful values, identities, invariants, ordering, or failure outcomes.

## Do Not Trigger When
Do not trigger when coverage is used only as a navigation signal and behavioral assertions independently prove the relevant contract.

## Distinguish From
weakened-test-to-pass removes meaningful expectations. test-implementation-coupled asserts the wrong surface. This rule mistakes traversal of code for proof about code.

## Decision Procedure
For each test, ask: which plausible defect makes this assertion fail? If no concrete defect can be named, the test is observation without judgment.

## Nudge
Execution is not verification. Assert a property whose violation would matter to a caller, and make the test capable of turning red under a realistic defect.
