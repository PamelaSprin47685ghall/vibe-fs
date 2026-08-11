# guessed-migration — Main

## What To Do Now
Replace heuristic legacy detection with explicit schema versions and deterministic migrations from each supported historical version. Durable schema identity is who owns migration: only a recorded version may authorize a deterministic transform; shape resemblance is not an owner.

## Why This Matters
Durable data is a conversation with past code. If the past did not record which language it was speaking, present code cannot safely infer meaning from resemblance. Silent guessing can produce valid-looking state whose provenance is false—the most dangerous kind of corruption because recovery appears successful.

## Repair Strategy
Establish authoritative version evidence. Write pure migration steps that transform one known schema to the next and test representative historical fixtures. For ambiguous unversioned data, fail closed or perform a separately authorized migration with documented assumptions.

## Decision Branches
- If durable evidence names the old version, apply the matching deterministic migration and persist the new version.
- If the old language cannot be proven, fail closed or run an explicit one-time authorized conversion; do not guess on every recovery.

## Common Wrong Fixes
- Do not stack more shape heuristics until fixtures pass. More guesses increase the number of ambiguous histories accepted as truth.
- Do not treat “it parsed under the new type” as proof the old bytes meant that type.
- Do not default unknown records to the latest schema because the fields look similar.

## Verification
The same old bytes must always produce the same upgraded value, independent of clock, environment, or current filesystem state, and unknown versions must be rejected explicitly. That determinism is the migration invariant: known version in, unique value out, unknown version refused.

## Done When
Recovery never asks “what schema does this look like?”; it reads a version and applies a defined semantic transformation.
