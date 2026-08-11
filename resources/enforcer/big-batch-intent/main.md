# big-batch-intent — Main

## What To Do Now
Decompose the oversized intent into outcomes with independent acceptance criteria. Execute unrelated units separately and bound any concurrency explicitly.

## Why This Matters
A task is an accountability boundary. If its parts have different owners or failure semantics, pretending they are one task destroys information: retry repeats work that already succeeded, review cannot isolate responsibility, and partial failure becomes an awkward exception instead of a representable state.

## Repair Strategy
Name each outcome, its input, its owner, and what proves it complete. Preserve ordering only where one outcome genuinely depends on another. Let independent units run independently, then combine their results at a deliberate join point.

## Wrong Fixes
Do not split mechanically by file count while preserving one ambiguous success condition. Do not replace one giant batch with unbounded fan-out. Decomposition is semantic, not merely smaller chunks.

## Verification
Each unit should have one clear completion claim and one clear failure surface. Retrying one unit must not require replaying unrelated successful work.

## Done When
The work graph exposes its real dependencies, independent outcomes can be judged independently, and no batch hides several incompatible notions of “done.”
