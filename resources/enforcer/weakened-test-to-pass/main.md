# weakened-test-to-pass — Main

## What To Do Now
Restore the strong assertion. Fix the product code. If the contract truly changed, update the spec and tests to the new stronger story—not a looser one that hides bugs.

## Repair Strategy
Diff the test change. Reintroduce removed cases. Confirm the failure mode. Patch implementation until the original behavioral intent holds.

## Decision Branches
If the old test was wrong, replace it with a correct strong test—not `assert true`. If flaky, fix determinism rather than deleting the check.

## Wrong Fixes
Deleting the only failing assertion. Broadening equality to "not null". Marking tests skipped to go green permanently.

## Verification
Restored tests fail on the old bug and pass on the fix; suite strength is not reduced.

## Done When
Behavioral expectations remain strong; the defect is fixed in implementation.

## Scope and Authority
Test changes responding to failures.
