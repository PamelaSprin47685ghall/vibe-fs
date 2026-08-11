# test-implementation-coupled — Main

## What To Do Now
Rewrite the test around supported inputs, observable outputs, durable state, and contractual external interactions; remove assertions on private decomposition. The supported public contract is who owns the testable invariant; private decomposition is not a promise the suite may freeze.

## Why This Matters
A good test permits internal evolution while forbidding behavioral regression. Implementation-coupled tests do the reverse: they punish refactoring and reward imitation of the old algorithm even when a simpler equivalent design exists.

## Repair Strategy
Identify the public promise behind each private assertion and observe that promise through the supported boundary. Keep interaction assertions only where count/order itself is part of the contract.

## Decision Branches
If the asserted detail is part of the supported contract, keep observing it at the public boundary.
If a conforming alternative implementation could violate it, drop or rewrite the assertion onto observable behavior.

## Common Wrong Fixes
- Expose private members solely to keep existing tests convenient.
- Replace private assertions with equally incidental snapshots of internal JSON.
- Delete the coupled test entirely instead of moving it to the observable contract.

## Verification
Invariant: the suite must stay green under a semantics-preserving internal refactor and turn red when promised behavior changes. Perform that refactor in thought or practice; only contract-level changes should fail the test.

## Done When
The suite constrains what users and neighboring components may rely on while leaving implementation details free to change without test surgery.
