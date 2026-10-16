# timeout-inflated-to-pass — Lesson

## Opinion

### Definition

A timeout is inflated to pass when a larger waiting budget is used as if it repaired the reason progress was late, absent, or nondeterministic.

The smell is not “someone changed 2s to 30s.” Timeout values legitimately change. The defect is causal substitution: **the clock is altered because the mechanism is not understood**.

### Governing Principle

A timeout does not make work progress. It decides how long the caller will tolerate uncertainty before declaring that progress has not been established.

If the real completion condition is a message, process exit, persisted record, lock release, readiness event, remote response, or state transition, then waiting longer can only give that condition more opportunity to happen. It cannot create the missing causal link.

This is why timeout inflation is such an effective cosmetic fix: the test turns green while the race, deadlock, leak, missing signal, pathological tail, or unbounded algorithm remains untouched. The defect becomes slower and harder to reproduce, which is often mistaken for improvement.

### Trigger When

Trigger when a failing or flaky operation is made acceptable primarily by increasing a timeout/deadline without evidence that the old bound was inconsistent with healthy expected latency. Typical cases:

- an integration test times out, so the limit is multiplied until CI usually passes;
- a process wait has no reliable completion signal, so the timeout becomes the de facto synchronization mechanism;
- an async race is masked by “giving it more time” rather than fixing ordering/ownership;
- a deadlock/resource leak is converted from a quick failure into a long hang;
- CI receives a much larger timeout than local runs solely because CI is “slow,” without measuring where the time goes;
- a model/agent proposes larger deadlines after every failure while no new causal observation is gathered;
- the chosen value is “the first number that turns green,” not a budget derived from service behavior or operational policy.

### Do Not Trigger When

- Measurement shows healthy p95/p99/tail latency legitimately exceeds the old bound, and the new timeout matches an explicit SLO or resource policy.
- A product/SLO decision intentionally changes how long the system is willing to wait for a still-progressing operation.
- The timeout is part of a negative test intentionally proving that absence of progress is bounded.
- A bounded remote retry/deadline accounts for documented tail behavior and the operation is demonstrably making causal progress.
- A test-specific timeout is raised because the test now intentionally performs more valid work, with the changed workload and bound both explicit.

### Distinguish From

`sleep-based-synchronization` inserts elapsed time as a substitute for readiness. `timeout-inflated-to-pass` moves the failure threshold outward so an unexplained wait is less visible.

`repeat-until-pass` buys more attempts; this rule buys more time per attempt. `resource-not-scoped`, cancellation, deadlock, and concurrency rules may own the underlying cause once found.

### Decision Procedure

Name the event that should make the operation complete.

Then ask:

1. Is that event causally connected to the wait, or are we merely hoping enough time passes?
2. Where was time actually spent in the failing run?
3. Was the operation making healthy progress, blocked, starved, leaked, or waiting on a missing signal?
4. What evidence justifies the new budget independently of “it passes now”?

If the only evidence for the larger timeout is that green became more likely, the rule applies.

### Examples

- positive: an integration test fails at 2s; timeout is changed to 30s; nobody determines that an event subscription is sometimes registered after the event fires.
- positive: CI occasionally hangs on a child process; the job timeout is raised from one minute to ten, while leaked children remain possible.
- positive: a browser test uses `waitForTimeout(5000)` and then raises the suite timeout because five seconds is sometimes insufficient.
- near-miss: telemetry shows a legitimate remote p99 of 1.8s against a 500ms policy; the service SLO is revised and the timeout becomes 2.5s with explicit margin.
- counterexample: the readiness event is fixed so the waiter wakes causally; the old timeout remains as a bounded failure policy.

## Advice

### What To Do Now

Put the clock back in its proper role.

Identify the causal event that should complete the operation and determine why that event was late or absent. Instrument the path if necessary. Repair the stalled mechanism first; only then choose a timeout from measured healthy behavior and the policy for how long uncertainty is worth tolerating.

### Why This Matters

Timeout inflation is attractive because it produces immediate cosmetic relief with almost no understanding.

That relief is frequently negative progress. A two-second race becomes a thirty-second race. A leaked child process becomes a ten-minute CI hang. A missing readiness signal becomes “slow infrastructure.” The system is not more reliable; the observation window has merely become too forgiving to expose the defect as often.

Timeouts are valuable precisely because they bound uncertainty. Treating them as a repair erases the boundary that was telling you something was wrong.

### Repair Strategy

Build a causal timeline instead of guessing a number:

- define the completion event;
- record when the operation starts;
- record meaningful milestones and resource acquisition;
- record whether the completion event is emitted, persisted, observed, or lost;
- record cancellation/cleanup behavior;
- distinguish CPU/work latency from blocked waiting;
- compare healthy and failing traces.

Repair ownership, ordering, signaling, cleanup, resource contention, or algorithmic cost wherever progress actually stops.

After the cause is understood, set the timeout from an explicit policy: measured tail latency plus justified margin, SLO, deadline budget, or bounded test expectation. A timeout should communicate how long healthy uncertainty is acceptable — not the smallest integer discovered by trial and error that happens to make CI green.

### Decision Branches

- **No causal progress is occurring:** do not raise the timeout. Fix the missing signal, deadlock, leak, starvation, or unbounded work.
- **Healthy progress exists but old budget contradicts measurement:** revise the timeout and document/encode the operational reason.
- **CI is slower because of known resource constraints:** measure the relevant path and either provision capacity, serialize legitimately contending heavy work, or use an environment-specific budget grounded in that measured constraint.
- **The test uses sleep as readiness:** replace sleep with a causal wait; see `sleep-based-synchronization`.
- **The operation has a real external tail:** model the deadline/retry policy explicitly and preserve final failure.

### Common Wrong Fixes

- Double the timeout repeatedly after each red until failure frequency becomes tolerable.
- Disable the timeout entirely. Infinite uncertainty is not reliability.
- Add retries on top of the larger timeout so every failure consumes more time before saying the same thing.
- Raise only CI limits and dismiss the difference as “CI is slow” without locating the resource or stage that is slow.
- Add progress logging but keep the missing causal signal unfixed. Observability helps diagnosis; it does not create completion.
- Use a huge timeout “for safety.” Huge budgets often hide orphaned work and make incident response worse.

### Verification

Demonstrate two independent facts:

1. **Causality:** healthy completion follows the intended event/condition, not merely elapsed time.
2. **Policy:** the timeout is large enough for measured healthy behavior and still small enough to bound actual failure intentionally.

Fault injection should still cause the operation to time out within the chosen bound. Healthy runs should complete because the causal condition occurred, not because the clock was generous.

### Done When

You can explain the timeout without saying “because this value passes.”

The mechanism owns progress. The clock only owns how long you are willing to wait before admitting progress was not established.

### Nudge

A larger clock cannot repair a missing cause.

First explain why progress was late. Then decide how long uncertainty deserves to live.
