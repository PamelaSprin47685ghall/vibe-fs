# sleep-based-synchronization — Main

## What To Do Now
Replace fixed sleeps with a wait on the real readiness, completion, notification, lock release, or observable state transition that the next step requires.

## Why This Matters
A sleep is simultaneously too long when the system is fast and too short when it is slow. It sacrifices latency without buying certainty. Causal signals adapt naturally because they encode the condition itself rather than an estimate of how long the condition usually takes.

## Repair Strategy
Expose an awaitable completion, poll a real state under a bounded timeout when no event exists, or use synchronization primitives whose semantics match the dependency. Keep timeout only as failure policy, never as success evidence.

## Wrong Fixes
Do not increase the delay until the flake becomes rare. Rarity is not correctness and load eventually finds the longer tail.

## Verification
Vary machine speed and inject long scheduling delays. The flow should advance immediately after the causal condition and never before it.

## Done When
Progress is licensed by an observable cause, while clocks only bound how long the system is willing to wait for that cause.
