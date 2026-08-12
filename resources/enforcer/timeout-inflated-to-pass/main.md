# timeout-inflated-to-pass — Main

## What To Do Now
Put the clock back in its proper role.

Identify the causal event that should complete the operation and determine why that event was late or absent. Instrument the path if necessary. Repair the stalled mechanism first; only then choose a timeout from measured healthy behavior and the policy for how long uncertainty is worth tolerating.

## Why This Matters
Timeout inflation is attractive because it produces immediate cosmetic relief with almost no understanding.

That relief is frequently negative progress. A two-second race becomes a thirty-second race. A leaked child process becomes a ten-minute CI hang. A missing readiness signal becomes “slow infrastructure.” The system is not more reliable; the observation window has merely become too forgiving to expose the defect as often.

Timeouts are valuable precisely because they bound uncertainty. Treating them as a repair erases the boundary that was telling you something was wrong.

## Repair Strategy
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

## Decision Branches
- **No causal progress is occurring:** do not raise the timeout. Fix the missing signal, deadlock, leak, starvation, or unbounded work.
- **Healthy progress exists but old budget contradicts measurement:** revise the timeout and document/encode the operational reason.
- **CI is slower because of known resource constraints:** measure the relevant path and either provision capacity, serialize legitimately contending heavy work, or use an environment-specific budget grounded in that measured constraint.
- **The test uses sleep as readiness:** replace sleep with a causal wait; see `sleep-based-synchronization`.
- **The operation has a real external tail:** model the deadline/retry policy explicitly and preserve final failure.

## Common Wrong Fixes
- Double the timeout repeatedly after each red until failure frequency becomes tolerable.
- Disable the timeout entirely. Infinite uncertainty is not reliability.
- Add retries on top of the larger timeout so every failure consumes more time before saying the same thing.
- Raise only CI limits and dismiss the difference as “CI is slow” without locating the resource or stage that is slow.
- Add progress logging but keep the missing causal signal unfixed. Observability helps diagnosis; it does not create completion.
- Use a huge timeout “for safety.” Huge budgets often hide orphaned work and make incident response worse.

## Verification
Demonstrate two independent facts:

1. **Causality:** healthy completion follows the intended event/condition, not merely elapsed time.
2. **Policy:** the timeout is large enough for measured healthy behavior and still small enough to bound actual failure intentionally.

Fault injection should still cause the operation to time out within the chosen bound. Healthy runs should complete because the causal condition occurred, not because the clock was generous.

## Done When
You can explain the timeout without saying “because this value passes.”

The mechanism owns progress. The clock only owns how long you are willing to wait before admitting progress was not established.
