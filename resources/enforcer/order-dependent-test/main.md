# order-dependent-test — Main

## What To Do Now
Make the test create every premise it needs and clean up every resource/state it owns within the test’s own lifecycle.

## Why This Matters
Suite order is not a business input. When a test depends on residue from another case, a hidden global state machine emerges and failures shift location as the runner parallelizes or reorders work. Isolation restores the meaning of a test as an independently reproducible claim.

## Repair Strategy
Reset or replace globals, use fresh storage/identities, scope environment changes, and stop sharing mutable fixtures across cases. If the steps truly form one lifecycle, combine them into one explicit scenario instead of pretending they are independent tests.

## Wrong Fixes
Do not force a fixed suite order. That codifies the hidden dependency instead of removing it.

## Verification
Run the case alone, first, last, and under randomized/parallel suite order where supported. Its verdict must be unchanged.

## Done When
Every test is self-contained evidence: its result is determined by its own explicit premises, not by history left behind by neighboring tests.
