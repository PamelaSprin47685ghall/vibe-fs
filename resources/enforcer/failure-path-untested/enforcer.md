# failure-path-untested — Enforcer

## Definition
A failure path is untested when newly introduced error handling, cancellation, rollback, retry, malformed-input, or recovery logic has never been forced to execute under test.

## Governing Principle
Failure code is usually least exercised in production until the moment correctness depends on it most. Its plausibility is therefore dangerous: branches that “obviously” release, rollback, or retry can remain dead assumptions for months. A failure path has no evidence merely because the happy path survives; it must be driven by the condition that gives it meaning.

## Trigger When
Trigger when code adds or changes a failure/recovery branch and no test creates the actual precondition that selects that branch and observes its externally relevant result.

## Do Not Trigger When
- An existing test already forces the exact failure mode through the same ownership boundary and protects its observable semantics.
- The change does not add or alter failure/recovery semantics (pure happy-path refactor with unchanged error contract).
- Exhaustive property coverage already includes the failure as a generated case with assertions on cleanup/state.
- The branch is unreachable production dead code; delete it rather than invent a test for abandoned handling.

## Distinguish From
`missing-regression-test` concerns a known defect. `coverage-theater` concerns weak assertions. This rule is specifically about unexecuted newly significant failure semantics. Tie-break: if the new rollback/retry/cancel path has never been forced, this rule owns the case even when line coverage is high.

## Decision Procedure
Name the failure, how it is induced, what cleanup/state/result must follow, and what must not happen. If no test demonstrates those four facts, the path is unproven.

## Examples
- positive: a new catch block “rolls back the reservation,” but every test only exercises successful checkout.
- near-miss: a test injects the reservation failure at the owning boundary and asserts rollback plus no charge.
- counterexample: add a test that induces the real failure and asserts result, cleanup, and forbidden side effects.

## Nudge
Failure handling is executable policy, not insurance prose. Force the real failure and prove its result, cleanup, and forbidden side effects.
