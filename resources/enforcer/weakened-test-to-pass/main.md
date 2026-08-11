# weakened-test-to-pass — Main

## What To Do Now
Restore the behavioral expectation unless a separate authoritative decision changed the contract; then fix the implementation until it satisfies the preserved test.

## Why This Matters
A test exists to make some implementations unacceptable. Weakening it solely because the current code fails removes exactly the pressure that gives verification value. The suite becomes a description of whatever the code already does rather than a constraint on what it is allowed to do.

## Repair Strategy
Recover the original requirement, isolate the failure mechanism, and repair production behavior. If the requirement truly changed, record that decision and rewrite the test to the new contract for that reason—not as a route to green.

## Wrong Fixes
Do not replace exact outcomes with broad truthiness, delete edge cases, or loosen fixtures without explaining the semantic requirement that disappeared.

## Verification
Temporarily restore the old defective implementation. The preserved/newly justified test should distinguish it from the required behavior for a contract-level reason.

## Done When
Green means the implementation satisfies an independently chosen contract, not that the contract was reduced until the implementation could satisfy it.
