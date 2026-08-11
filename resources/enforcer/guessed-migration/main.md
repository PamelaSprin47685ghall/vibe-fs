# guessed-migration — Main

## What To Do Now
Replace heuristic legacy detection with explicit schema versions and deterministic migrations from each supported historical version.

## Why This Matters
Durable data is a conversation with past code. If the past did not record which language it was speaking, present code cannot safely infer meaning from resemblance. Silent guessing can produce valid-looking state whose provenance is false—the most dangerous kind of corruption because recovery appears successful.

## Repair Strategy
Establish authoritative version evidence. Write pure migration steps that transform one known schema to the next and test representative historical fixtures. For ambiguous unversioned data, fail closed or perform a separately authorized migration with documented assumptions.

## Wrong Fixes
Do not stack more shape heuristics until fixtures pass. More guesses increase the number of ambiguous histories accepted as truth.

## Verification
The same old bytes must always produce the same upgraded value, independent of clock, environment, or current filesystem state, and unknown versions must be rejected explicitly.

## Done When
Recovery never asks “what schema does this look like?”; it reads a version and applies a defined semantic transformation.
