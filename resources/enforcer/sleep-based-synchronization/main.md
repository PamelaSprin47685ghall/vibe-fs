# sleep-based-synchronization — Main

## What To Do Now
Replace fixed sleeps with a wait on the real readiness, completion, notification, lock release, or observable state transition that the next step requires.

## Why This Matters
A sleep is simultaneously too long when the system is fast and too short when it is slow. It sacrifices latency without buying certainty. Causal signals adapt naturally because they encode the condition itself rather than an estimate of how long the condition usually takes.

## Repair Strategy
Expose an awaitable completion, poll a real state under a bounded timeout when no event exists, or use synchronization primitives whose semantics match the dependency. Keep timeout only as failure policy, never as success evidence.

## Decision Branches
- If a completion/readiness event can be exposed, wait on that event.
- If only state is observable, poll that state with a timeout that fails rather than succeeding on expiry.
- If the sleep is backoff or rate limiting, keep it and ensure success still depends on a causal signal.

## Common Wrong Fixes
- Increase the delay until the flake becomes rare.
- Replace `sleep` with a busy loop of the same duration.
- Keep the sleep and add a retry “just in case.”
- Use a longer CI timeout as a substitute for a readiness probe.

## Verification
Vary machine speed and inject long scheduling delays. The flow should advance immediately after the causal condition and never before it. The invariant is: progress is licensed by an observable cause; clocks only bound wait, they never prove success.

## Done When
Progress is licensed by an observable cause, while clocks only bound how long the system is willing to wait for that cause.
