# canary-skipped — Main

## What To Do Now
Write the external assumption down precisely and test it against the real Host or provider with the smallest safe canary that can prove it false.

## Why This Matters
Mocks prove what we told them to do. A canary proves what the other system actually does. If the behavior is undocumented, the gap between those two statements is the entire risk. Internal tests can be perfectly green while resting on a false premise about framing, ordering, identity, or lifecycle.

## Repair Strategy
Keep pure and contract tests for fast reasoning, then add one narrow real-boundary check for the irreducible empirical assumption. Make the canary stable, cheap, and explicit about what observation constitutes failure.

## Wrong Fixes
Do not widen mocks until they mimic the behavior you hope exists. Do not cite comments or historical observations as current proof when the environment is the authority.

## Verification
The canary must fail if the external assumption changes while the internal implementation remains untouched.

## Done When
The release no longer depends on an untested empirical premise: lower layers prove internal logic, and the real boundary proves the behavior only it can own.
