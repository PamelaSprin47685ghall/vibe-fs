# weakened-test-to-pass — Main

## What To Do Now
Restore the behavioral expectation unless a separate authoritative decision changed the contract; then fix the implementation until it satisfies the preserved test. Change the test only if who owns the contract authorized a new promise.

## Why This Matters
A test exists to make some implementations unacceptable. Weakening it solely because the current code fails removes exactly the pressure that gives verification value. The suite becomes a description of whatever the code already does rather than a constraint on what it is allowed to do.

## Repair Strategy
Recover the original requirement, isolate the failure mechanism, and repair production behavior. If the requirement truly changed, record that decision and rewrite the test to the new contract for that reason—not as a route to green.

## Decision Branches
If no independent contract change exists, restore the expectation and fix the implementation.
If an authorized contract change exists, rewrite the test to the new promise for that reason, not to silence a failure.

## Common Wrong Fixes
- Replace exact outcomes with broad truthiness, delete edge cases, or loosen fixtures without a disappeared requirement.
- Mark the test skipped or flaky instead of settling the disagreement.
- Assert only that no exception was thrown when the original claim was a specific result.

## Verification
Invariant: green means the implementation satisfies an independently chosen contract. Temporarily restore the old defective implementation; the preserved or newly justified test should distinguish it from the required behavior for a contract-level reason.

## Done When
Green means the implementation satisfies an independently chosen contract, not that the contract was reduced until the implementation could satisfy it.
