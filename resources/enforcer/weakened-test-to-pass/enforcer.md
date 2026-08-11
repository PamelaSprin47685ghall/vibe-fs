# weakened-test-to-pass — Enforcer

## Definition
A test is weakened to pass when assertions, cases, fixtures, or expected outcomes are removed or relaxed primarily because the implementation fails them, without an independently justified change to the contract.

## Governing Principle
A failing test is evidence of disagreement between implementation and specification. There are only two legitimate resolutions: the implementation is wrong, or the specification has intentionally changed. The root-cause is resolving spec/impl disagreement by silencing the witness. Weakening the test merely because red is inconvenient erases evidence instead of settling the disagreement. It converts verification from an adversary of defects into a servant of the current implementation.

## Trigger When
Trigger when meaningful expectations are loosened, edge cases deleted, assertions generalized, or fixtures simplified chiefly to make an otherwise failing implementation green.

## Do Not Trigger When
- An explicit product/contract decision changed the required behavior and the old expectation is demonstrably obsolete.
- Assertions that were never contractual are removed as `test-implementation-coupled` repair, with the public promise still tested.
- The suite is tightened after discovering a stronger contract, not relaxed to match a defect.
- Flake caused by time or order is fixed by controlling inputs rather than deleting the behavioral claim.

## Distinguish From
`test-implementation-coupled` removes assertions that should never have been contractual. `coverage-theater` lacks strong assertions to begin with. Tie-break: if a valid behavioral claim was abandoned under pressure from a failure, use this rule; if the assertion froze private choreography, use `test-implementation-coupled`.

## Decision Procedure
Before changing the test, state which contract changed and where that decision is authorized. If no independent contract change exists, preserve the expectation and repair the implementation.

## Examples
- positive: an overflow case is deleted because the new code throws, and the suite goes green.
- near-miss: product records that overflow is now rejected at the API, and the test is rewritten to that new contract.
- counterexample: dropping a spy on a private helper while keeping the observable result is `test-implementation-coupled` repair.

## Nudge
Do not resolve disagreement by silencing the witness. Change a test only because the contract changed—not because the implementation would prefer a weaker question.
