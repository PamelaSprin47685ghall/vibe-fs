# missing-regression-test — Main

## What To Do Now
Capture the reported defect as a behavioral test that fails against the old mechanism and passes only after the repair.

## Why This Matters
A fix changes code; a regression test changes institutional memory. The test turns a one-time debugging discovery into a permanent constraint on future implementations, preventing the same failure from becoming expensive knowledge twice.

## Repair Strategy
Use the smallest input that demonstrates the original bug through its owning boundary. Assert the externally meaningful wrong/right outcome, not the incidental implementation detail that happened to cause it.

## Wrong Fixes
Do not write a test that can only run against the new structure, or one that merely exercises the repaired line. It must distinguish the buggy behavior from the correct contract.

## Verification
Temporarily restore the old defect or mutation and confirm the test turns red for the expected reason.

## Done When
The repository contains an executable memory of the bug, and any future implementation that recreates it must fail before delivery.
