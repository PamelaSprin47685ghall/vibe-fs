# missing-regression-test — Main

## What To Do Now
Capture the reported defect as a behavioral test that fails against the old mechanism and passes only after the repair.

## Why This Matters
A fix changes code; a regression test changes institutional memory. The test turns a one-time debugging discovery into a permanent constraint on future implementations, preventing the same failure from becoming expensive knowledge twice.

## Repair Strategy
Use the smallest input that demonstrates the original bug through its owning boundary. Assert the externally meaningful wrong/right outcome, not the incidental implementation detail that happened to cause it.

## Decision Branches
- If the defect is still reproducible, write the failing behavioral test first, then apply the fix until that test is green.
- If the production code is already patched, restore or mutate the old mechanism long enough to prove the new test would have caught it.

## Common Wrong Fixes
- Write a test that can only run against the new structure and never distinguished the buggy behavior.
- Assert only that the repaired line is executed, not the externally meaningful outcome.
- Snapshot incidental internals so the test breaks on harmless refactors without guarding the original bug.

## Verification
Temporarily restore the old defect or mutation and confirm the test turns red for the expected reason. The invariant is that recreating the reported failure fails before delivery.

## Done When
The repository contains an executable memory of the bug, and any future implementation that recreates it must fail before delivery.
