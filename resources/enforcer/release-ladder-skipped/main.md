# release-ladder-skipped — Main

## What To Do Now
Run the applicable verification rungs in order: pure behavior, contract boundaries, replay/recovery, then real-environment canary or release checks where relevant. Each applicable narrower rung is who owns its class of uncertainty; a later integration or canary is not who owns the skipped local proof.

## Why This Matters
A broad test mixes many causes. When it fails, diagnosis is expensive; when it passes, the specific local invariant may still have escaped exercise. Narrow proofs establish small truths first, so later stages test only what cannot be proven below.

## Repair Strategy
Map the change surface to proof levels and clear each applicable gate before promotion. Keep every discovered regression at the lowest level that can express it faithfully.

## Decision Branches
- If the change touches pure logic, clear the local/unit (and property) rung before integration.
- If the change touches a boundary, add or run contract tests before a full environment.
- If lower rungs are inapplicable, record why and run only the remaining applicable ladder.

## Common Wrong Fixes
- Compensate for missing unit/contract proof by running a larger end-to-end suite repeatedly.
- Skip local tests because “CI will catch it” at a later stage.
- Treat a green canary as proof of an untested local algebra.
- Disable a failing lower rung to reach the next stage faster.

## Verification
Each rung should be green through the project’s standard command before the next rung is treated as meaningful evidence. The invariant is: each applicable narrower proof is established before a broader stage is counted as completion evidence.

## Done When
Completion is supported by an ordered chain of proofs, each responsible for the class of uncertainty only that level can settle.
