# big-batch-intent — Main

## What To Do Now
Decompose the oversized intent into outcomes with independent acceptance criteria. Execute unrelated units separately and bound any concurrency explicitly. Each independently accept-or-fail outcome is who owns its own completion claim; a batch coordinator is not who owns those separate truths.

## Why This Matters
A task is an accountability boundary. If its parts have different owners or failure semantics, pretending they are one task destroys information: retry repeats work that already succeeded, review cannot isolate responsibility, and partial failure becomes an awkward exception instead of a representable state.

## Repair Strategy
Name each outcome, its input, its owner, and what proves it complete. Preserve ordering only where one outcome genuinely depends on another. Let independent units run independently, then combine their results at a deliberate join point.

## Decision Branches
- If outcomes can succeed or fail independently, split them into separate units before execution.
- If a real invariant requires all-or-nothing completion, keep those steps together and name the atomic promise.
- If units are independent after the split, run them concurrently only with an explicit join and bounded fan-out.

## Common Wrong Fixes
- Do not split mechanically by file count while preserving one ambiguous success condition.
- Do not replace one giant batch with unbounded fan-out.
- Do not hide several “done” meanings behind a single status flag.
- Do not retry the whole bundle because one independent part failed.

## Verification
Each unit should have one clear completion claim and one clear failure surface. Retrying one unit must not require replaying unrelated successful work. The invariant is that the unit of execution matches the unit of truth: mixed success is representable only where the domain actually treats the work as atomic.

## Done When
The work graph exposes its real dependencies, independent outcomes can be judged independently, and no batch hides several incompatible notions of “done.”
