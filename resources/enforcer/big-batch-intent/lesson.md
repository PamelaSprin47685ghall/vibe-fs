# big-batch-intent — Lesson

## Opinion

### Definition

A batch is too large when one instruction contains several outcomes whose correctness, ownership, or failure can be judged independently. The root-cause is that several independently judgeable outcomes are fused into one instruction, so the unit of execution outgrows the unit of truth and mixed success has no honest state.

### Governing Principle

An operation should have one coherent success condition. When unrelated intents are bundled, the unit of execution becomes larger than the unit of truth: one part can succeed while another fails, yet the batch offers no honest state for that mixed result. Reviewability, retry, concurrency, and rollback all become ambiguous because the system no longer knows what the atomic promise is.

### Trigger When

Trigger when a task or tool call combines independent searches, edits, migrations, validations, or decisions merely for convenience, especially when they could proceed concurrently or be reviewed separately.

### Do Not Trigger When

- The steps jointly establish one invariant and partial completion would be meaningless or unsafe.
- The work is one transaction whose intermediate states are not valid outcomes for callers.
- Independent-looking files are actually one semantic change with a single acceptance criterion.
- A fan-out is an implementation of one named outcome, not several bundled intents.

### Distinguish From

`scope-creep` adds unjustified work. `serial-when-parallel` executes independent work sequentially. This rule concerns an intent whose semantic boundary is already too broad before execution begins. Tie-break: if the instruction already mixes independent success conditions, this rule owns it even before scheduling is chosen.

### Decision Procedure

1. List the observable outcomes requested.
2. Ask whether each can succeed or fail independently.
3. Split along those independent truth conditions.
4. Recompose only where a real invariant requires atomicity.

### Examples

- positive: one tool call searches docs, edits three unrelated modules, and runs a migration as a single “done.”
- near-miss: a multi-file rename that must land together because partial rename would not compile.
- counterexample: each independent outcome is its own task with its own acceptance and failure surface.

## Advice

### What To Do Now

Decompose the oversized intent into outcomes with independent acceptance criteria. Execute unrelated units separately and bound any concurrency explicitly. Each independently accept-or-fail outcome is who owns its own completion claim; a batch coordinator is not who owns those separate truths.

### Why This Matters

A task is an accountability boundary. If its parts have different owners or failure semantics, pretending they are one task destroys information: retry repeats work that already succeeded, review cannot isolate responsibility, and partial failure becomes an awkward exception instead of a representable state.

### Repair Strategy

Name each outcome, its input, its owner, and what proves it complete. Preserve ordering only where one outcome genuinely depends on another. Let independent units run independently, then combine their results at a deliberate join point.

### Decision Branches

- If outcomes can succeed or fail independently, split them into separate units before execution.
- If a real invariant requires all-or-nothing completion, keep those steps together and name the atomic promise.
- If units are independent after the split, run them concurrently only with an explicit join and bounded fan-out.

### Common Wrong Fixes

- Do not split mechanically by file count while preserving one ambiguous success condition.
- Do not replace one giant batch with unbounded fan-out.
- Do not hide several “done” meanings behind a single status flag.
- Do not retry the whole bundle because one independent part failed.

### Verification

Each unit should have one clear completion claim and one clear failure surface. Retrying one unit must not require replaying unrelated successful work. The invariant is that the unit of execution matches the unit of truth: mixed success is representable only where the domain actually treats the work as atomic.

### Done When

The work graph exposes its real dependencies, independent outcomes can be judged independently, and no batch hides several incompatible notions of “done.”

### Nudge

Make the unit of execution match the unit of truth. Split independent outcomes; keep only genuinely atomic work together.
