# scope-creep — Main

## What To Do Now
Remove unrelated edits from the current delivery and keep only changes that follow directly from the requested outcome or an invariant that outcome necessarily disturbs.

## Why This Matters
Broad changes make causality expensive. When behavior, cleanup, architecture, and migration move together, reviewers cannot tell which edit proves which requirement and regressions inherit a much larger suspect set. Small coherent scope is a reasoning optimization.

## Repair Strategy
Map each changed area to a requirement. Preserve necessary transitive edits, but defer independent improvements as separate work with their own acceptance criteria.

## Wrong Fixes
Do not keep unrelated changes because they are individually good. Correctness of an edit does not establish relevance to this change.

## Verification
The final diff should admit a concise explanation from every material edit back to the task’s contract, with no branch of work requiring a separate motivation.

## Done When
The delivery is complete without being expansive: every edit participates in the same causal story, and unrelated opportunities remain separate rather than hidden inside it.
