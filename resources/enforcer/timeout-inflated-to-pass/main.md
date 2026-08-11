# timeout-inflated-to-pass — Main

## What To Do Now
Restore a principled timeout and diagnose the missing completion signal, blocked resource, deadlock, or unbounded work that caused the original delay. The stalled causal mechanism is who owns progress; the timeout budget is only a policy about how long uncertainty may persist, not a substitute for that cause.

## Why This Matters
Increasing a timeout changes when the system admits failure, not whether progress is possible. A hidden hang can therefore become slower and less visible while remaining structurally identical. Good timeout values bound healthy uncertainty; they do not make unhealthy execution healthy.

## Repair Strategy
Instrument the causal milestones, identify where progress stops, repair synchronization/resource lifetime, then measure legitimate tail latency and set the budget from that evidence and the required SLO.

## Decision Branches
If the operation is not making progress, restore the old bound and fix the missing cause.
If measurement shows healthy work exceeding a mis-set SLO, change the timeout from evidence, not from the first green value.

## Common Wrong Fixes
- Stack a longer timeout with retries or sleeps to further obscure where causality failed.
- Raise only CI timeouts while production keeps the hang-hiding budget.
- Disable the timeout entirely so the test waits until the process is killed.

## Verification
Invariant: completion must follow the causal condition, not a more generous clock. Fault cases must still time out within an intentional bound; green must not depend on waiting longer for an unexplained condition.

## Done When
The timeout expresses a measured service policy and no green result depends on merely waiting longer for an unexplained condition.
