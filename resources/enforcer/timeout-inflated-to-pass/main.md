# timeout-inflated-to-pass — Main

## What To Do Now
Restore a principled timeout and diagnose the missing completion signal, blocked resource, deadlock, or unbounded work that caused the original delay.

## Why This Matters
Increasing a timeout changes when the system admits failure, not whether progress is possible. A hidden hang can therefore become slower and less visible while remaining structurally identical. Good timeout values bound healthy uncertainty; they do not make unhealthy execution healthy.

## Repair Strategy
Instrument the causal milestones, identify where progress stops, repair synchronization/resource lifetime, then measure legitimate tail latency and set the budget from that evidence and the required SLO.

## Wrong Fixes
Do not stack a longer timeout with retries or sleeps. That compounds waiting while further obscuring the point where causality failed.

## Verification
The operation should complete because its causal condition occurs, not because the new budget is generous. Fault cases must still time out within an intentional bound.

## Done When
The timeout expresses a measured service policy and no green result depends on merely waiting longer for an unexplained condition.
