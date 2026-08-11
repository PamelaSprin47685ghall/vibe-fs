# ignored-tdd — Main

## What To Do Now
Write the behavioral test first, run it red for the intended reason, then make the smallest production change that turns that same test green.

## Why This Matters
A post-hoc test can be perfectly aligned with an accidental implementation because the implementation has already shaped what the author thinks to assert. Red-first creates a counterfactual baseline: this requirement is demonstrably not met before the code changes.

## Repair Strategy
Express the requirement at the public behavioral boundary, avoid assertions on private structure, and keep the first failure as evidence that the test detects the missing behavior rather than a fixture mistake.

## Wrong Fixes
Do not write a test after implementation and assume it would have failed before. Do not weaken the test when the implementation resists it; revisit the contract or the code instead.

## Verification
The test must be able to distinguish old behavior from new behavior and remain meaningful after internal refactoring.

## Done When
Requirement, failure, implementation, and proof form a causal sequence: red identifies the gap, green closes it, and refactoring preserves the behavior.
