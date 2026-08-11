# failure-path-untested — Enforcer

## Definition
A failure path is untested when newly introduced error handling, cancellation, rollback, retry, malformed-input, or recovery logic has never been forced to execute under test.

## Governing Principle
Failure code is usually least exercised in production until the moment correctness depends on it most. Its plausibility is therefore dangerous: branches that “obviously” release, rollback, or retry can remain dead assumptions for months. A failure path has no evidence merely because the happy path survives; it must be driven by the condition that gives it meaning.

## Trigger When
Trigger when code adds or changes a failure/recovery branch and no test creates the actual precondition that selects that branch and observes its externally relevant result.

## Do Not Trigger When
Do not trigger when an existing test already forces the exact failure mode through the same ownership boundary and protects its observable semantics.

## Distinguish From
missing-regression-test concerns a known defect. coverage-theater concerns weak assertions. This rule is specifically about unexecuted newly significant failure semantics.

## Decision Procedure
Name the failure, how it is induced, what cleanup/state/result must follow, and what must not happen. If no test demonstrates those four facts, the path is unproven.

## Nudge
Failure handling is executable policy, not insurance prose. Force the real failure and prove its result, cleanup, and forbidden side effects.
