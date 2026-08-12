# timeout-inflated-to-pass — Enforcer

## Definition
A timeout is inflated to pass when a larger waiting budget is used as if it repaired the reason progress was late, absent, or nondeterministic.

The smell is not “someone changed 2s to 30s.” Timeout values legitimately change. The defect is causal substitution: **the clock is altered because the mechanism is not understood**.

## Governing Principle
A timeout does not make work progress. It decides how long the caller will tolerate uncertainty before declaring that progress has not been established.

If the real completion condition is a message, process exit, persisted record, lock release, readiness event, remote response, or state transition, then waiting longer can only give that condition more opportunity to happen. It cannot create the missing causal link.

This is why timeout inflation is such an effective cosmetic fix: the test turns green while the race, deadlock, leak, missing signal, pathological tail, or unbounded algorithm remains untouched. The defect becomes slower and harder to reproduce, which is often mistaken for improvement.

## Trigger When
Trigger when a failing or flaky operation is made acceptable primarily by increasing a timeout/deadline without evidence that the old bound was inconsistent with healthy expected latency. Typical cases:

- an integration test times out, so the limit is multiplied until CI usually passes;
- a process wait has no reliable completion signal, so the timeout becomes the de facto synchronization mechanism;
- an async race is masked by “giving it more time” rather than fixing ordering/ownership;
- a deadlock/resource leak is converted from a quick failure into a long hang;
- CI receives a much larger timeout than local runs solely because CI is “slow,” without measuring where the time goes;
- a model/agent proposes larger deadlines after every failure while no new causal observation is gathered;
- the chosen value is “the first number that turns green,” not a budget derived from service behavior or operational policy.

## Do Not Trigger When
- Measurement shows healthy p95/p99/tail latency legitimately exceeds the old bound, and the new timeout matches an explicit SLO or resource policy.
- A product/SLO decision intentionally changes how long the system is willing to wait for a still-progressing operation.
- The timeout is part of a negative test intentionally proving that absence of progress is bounded.
- A bounded remote retry/deadline accounts for documented tail behavior and the operation is demonstrably making causal progress.
- A test-specific timeout is raised because the test now intentionally performs more valid work, with the changed workload and bound both explicit.

## Distinguish From
`sleep-based-synchronization` inserts elapsed time as a substitute for readiness. `timeout-inflated-to-pass` moves the failure threshold outward so an unexplained wait is less visible.

`repeat-until-pass` buys more attempts; this rule buys more time per attempt. `resource-not-scoped`, cancellation, deadlock, and concurrency rules may own the underlying cause once found.

## Decision Procedure
Name the event that should make the operation complete.

Then ask:

1. Is that event causally connected to the wait, or are we merely hoping enough time passes?
2. Where was time actually spent in the failing run?
3. Was the operation making healthy progress, blocked, starved, leaked, or waiting on a missing signal?
4. What evidence justifies the new budget independently of “it passes now”?

If the only evidence for the larger timeout is that green became more likely, the rule applies.

## Examples
- positive: an integration test fails at 2s; timeout is changed to 30s; nobody determines that an event subscription is sometimes registered after the event fires.
- positive: CI occasionally hangs on a child process; the job timeout is raised from one minute to ten, while leaked children remain possible.
- positive: a browser test uses `waitForTimeout(5000)` and then raises the suite timeout because five seconds is sometimes insufficient.
- near-miss: telemetry shows a legitimate remote p99 of 1.8s against a 500ms policy; the service SLO is revised and the timeout becomes 2.5s with explicit margin.
- counterexample: the readiness event is fixed so the waiter wakes causally; the old timeout remains as a bounded failure policy.

## Nudge
A larger clock cannot repair a missing cause.

First explain why progress was late. Then decide how long uncertainty deserves to live.
