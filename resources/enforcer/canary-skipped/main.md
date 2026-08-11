# canary-skipped — Main

## What To Do Now
Write the external assumption down precisely and test it against the real Host or provider with the smallest safe canary that can prove it false.

## Why This Matters
Mocks prove what we told them to do. A canary proves what the other system actually does. If the behavior is undocumented, the gap between those two statements is the entire risk. Internal tests can be perfectly green while resting on a false premise about framing, ordering, identity, or lifecycle.

## Repair Strategy
Keep pure and contract tests for fast reasoning, then add one narrow real-boundary check for the irreducible empirical assumption. Make the canary stable, cheap, and explicit about what observation constitutes failure.

## Decision Branches
- If the premise is undocumented Host behavior, add a narrow real canary before release.
- If a stable declared contract already has an equivalent failing-capable test, do not duplicate a live canary for that same declared fact.
- If the change never reaches the Host boundary, skip the canary; the empirical premise is not in play.

## Common Wrong Fixes
- Do not widen mocks until they mimic the behavior you hope exists.
- Do not cite comments or historical observations as current proof when the environment is the authority.
- Do not run a broad end-to-end suite that never asserts the specific empirical fact.
- Do not treat a passing staging deploy with no targeted observation as a canary.

## Verification
The canary must fail if the external assumption changes while the internal implementation remains untouched. The invariant is that no release depends on an untested empirical premise owned only by the real Host.

## Done When
The release no longer depends on an untested empirical premise: lower layers prove internal logic, and the real boundary proves the behavior only it can own.
