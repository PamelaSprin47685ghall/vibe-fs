# weakened-test-to-pass — Enforcer

## Definition
A test is weakened to pass when assertions, cases, fixtures, or expected outcomes are removed or relaxed primarily because the implementation fails them, without an independently justified change to the contract.

## Governing Principle
A failing test is evidence of disagreement between implementation and specification. There are only two legitimate resolutions: the implementation is wrong, or the specification has intentionally changed. Weakening the test merely because red is inconvenient erases the witness instead of settling the disagreement. It converts verification from an adversary of defects into a servant of the current implementation.

## Trigger When
Trigger when meaningful expectations are loosened, edge cases deleted, assertions generalized, or fixtures simplified chiefly to make an otherwise failing implementation green.

## Do Not Trigger When
Do not trigger when an explicit product/contract decision changed the required behavior and the old expectation is demonstrably obsolete.

## Distinguish From
test-implementation-coupled removes assertions that should never have been contractual. coverage-theater lacks strong assertions to begin with. This rule abandons a valid behavioral claim under pressure from a failure.

## Decision Procedure
Before changing the test, state which contract changed and where that decision is authorized. If no independent contract change exists, preserve the expectation and repair the implementation.

## Nudge
Do not resolve disagreement by silencing the witness. Change a test only because the contract changed—not because the implementation would prefer a weaker question.
