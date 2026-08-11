# non-exhaustive-transition — Main

## What To Do Now
Write the transition relation exhaustively: every reachable state and input pair must either produce a named successor/result or an explicit typed rejection.

## Why This Matters
A wildcard in a finite state machine is a policy decision without a name. It silently grants the same semantics to cases that may deserve different treatment now or after the model grows. Exhaustiveness converts missing decisions into compile-time or test-time pressure.

## Repair Strategy
Model states/events as closed cases, enumerate transitions, and keep illegal transitions explicit rather than dropping them. Let exhaustive matching force review whenever a new case is introduced.

## Wrong Fixes
Do not route unspecified transitions to “ignore” or “invalid” without deciding whether each pair is truly equivalent. Generic rejection can hide legitimate future behavior just as generic success can.

## Verification
Build a table/property test over all finite pairs and assert the expected successor or rejection for each.

## Done When
The transition function is a complete readable specification of the state machine, with no semantic cells filled by default control flow.
