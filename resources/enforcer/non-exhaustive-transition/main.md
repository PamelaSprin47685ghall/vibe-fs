# non-exhaustive-transition — Main

## What To Do Now
Write the transition relation exhaustively: every reachable state and input pair must either produce a named successor/result or an explicit typed rejection. The closed transition function is who owns every cell of the relation; a wildcard is not a decision.

## Why This Matters
A wildcard in a finite state machine is a policy decision without a name. It silently grants the same semantics to cases that may deserve different treatment now or after the model grows. Exhaustiveness converts missing decisions into compile-time or test-time pressure.

## Repair Strategy
Model states/events as closed cases, enumerate transitions, and keep illegal transitions explicit rather than dropping them. Let exhaustive matching force review whenever a new case is introduced.

## Decision Branches
- If the state/event set is closed, enumerate every pair as a named successor or typed rejection and forbid wildcards.
- If the protocol is intentionally open, document the unknown-case law once and keep it out of finite domain transitions.

## Common Wrong Fixes
- Route unspecified transitions to "ignore" or "invalid" without deciding whether each pair is truly equivalent.
- Add a default that logs and continues, hiding missing cells.
- Expand the wildcard with comments listing intended cases instead of making those cases compile-time obligations.

## Verification
Build a table/property test over all finite pairs and assert the expected successor or rejection for each. The invariant is that every cell of the transition relation is an explicit domain decision.

## Done When
The transition function is a complete readable specification of the state machine, with no semantic cells filled by default control flow.
