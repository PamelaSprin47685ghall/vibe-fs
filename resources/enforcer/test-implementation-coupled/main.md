# test-implementation-coupled — Main

## What To Do Now
Rewrite the test around supported inputs, observable outputs, durable state, and contractual external interactions; remove assertions on private decomposition.

## Why This Matters
A good test permits internal evolution while forbidding behavioral regression. Implementation-coupled tests do the reverse: they punish refactoring and reward imitation of the old algorithm even when a simpler equivalent design exists.

## Repair Strategy
Identify the public promise behind each private assertion and observe that promise through the supported boundary. Keep interaction assertions only where count/order itself is part of the contract.

## Wrong Fixes
Do not expose private members solely to keep existing tests convenient. That turns test knowledge into production API surface.

## Verification
Perform a semantics-preserving refactor of internal helpers in thought or practice. The test should stay green; change the promised behavior and it should turn red.

## Done When
The suite constrains what users and neighboring components may rely on while leaving implementation details free to change without test surgery.
