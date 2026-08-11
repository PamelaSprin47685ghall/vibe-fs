# order-dependent-test — Main

## What To Do Now
Make the test create every premise it needs and clean up every resource/state it owns within the test's own lifecycle. The test case's own setup and teardown is who owns the isolation invariant that a verdict depends only on that case's explicit premises.

## Why This Matters
Suite order is not a business input. When a test depends on residue from another case, a hidden global state machine emerges and failures shift location as the runner parallelizes or reorders work. Isolation restores the meaning of a test as an independently reproducible claim.

## Repair Strategy
Reset or replace globals, use fresh storage/identities, scope environment changes, and stop sharing mutable fixtures across cases. If the steps truly form one lifecycle, combine them into one explicit scenario instead of pretending they are independent tests.

## Decision Branches
- If cases must share a lifecycle, merge them into one explicit scenario test.
- If they are independent claims, give each fresh premises and dispose owned state in its own scope.

## Common Wrong Fixes
- Force a fixed suite order, which codifies the hidden dependency instead of removing it.
- Add more shared `beforeAll` mutation so later tests appear green.
- Skip isolation under "faster CI" while leaving residue in place.

## Verification
Run the case alone, first, last, and under randomized/parallel suite order where supported. Its verdict must be unchanged. The invariant is that a test's result depends only on its own explicit premises.

## Done When
Every test is self-contained evidence: its result is determined by its own explicit premises, not by history left behind by neighboring tests.
