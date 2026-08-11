# release-ladder-skipped — Main

## What To Do Now
Run the applicable verification rungs in order: pure behavior, contract boundaries, replay/recovery, then real-environment canary or release checks where relevant.

## Why This Matters
A broad test mixes many causes. When it fails, diagnosis is expensive; when it passes, the specific local invariant may still have escaped exercise. Narrow proofs establish small truths first, so later stages test only what cannot be proven below.

## Repair Strategy
Map the change surface to proof levels and clear each applicable gate before promotion. Keep every discovered regression at the lowest level that can express it faithfully.

## Wrong Fixes
Do not compensate for missing unit/contract proof by running a larger end-to-end suite repeatedly. More environment does not imply more precision.

## Verification
Each rung should be green through the project’s standard command before the next rung is treated as meaningful evidence.

## Done When
Completion is supported by an ordered chain of proofs, each responsible for the class of uncertainty only that level can settle.
