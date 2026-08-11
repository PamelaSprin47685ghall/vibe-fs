# scope-creep — Main

## What To Do Now
Remove unrelated edits from the current delivery and keep only changes that follow directly from the requested outcome or an invariant that outcome necessarily disturbs.

## Why This Matters
Broad changes make causality expensive. When behavior, cleanup, architecture, and migration move together, reviewers cannot tell which edit proves which requirement and regressions inherit a much larger suspect set. Small coherent scope is a reasoning optimization.

## Repair Strategy
Map each changed area to a requirement. Preserve necessary transitive edits, but defer independent improvements as separate work with their own acceptance criteria.

## Decision Branches
- If an edit has no chain to the acceptance criteria or a disturbed invariant, move it out of this delivery.
- If an edit is required for compilation or invariant restore after the intended change, keep it and name that necessity.
- If the work has become a rewrite of an adjacent subsystem, split a new change rather than enlarge this one.

## Common Wrong Fixes
- Keep unrelated changes because they are individually good.
- Bundle “drive-by” formatting or dependency bumps to “reduce PR count.”
- Expand tests to cover untouched modules as a substitute for relevance.
- Leave the extra work in and add a paragraph of justification after the fact.

## Verification
The final diff should admit a concise explanation from every material edit back to the task’s contract, with no branch of work requiring a separate motivation. The invariant is: every material edit is a consequence of the stated outcome or a necessary restore of an invariant that outcome disturbs.

## Done When
The delivery is complete without being expansive: every edit participates in the same causal story, and unrelated opportunities remain separate rather than hidden inside it.
