# big-batch-intent — Enforcer

## Definition
A batch is too large when one instruction contains several outcomes whose correctness, ownership, or failure can be judged independently.

## Governing Principle
An operation should have one coherent success condition. When unrelated intents are bundled, the unit of execution becomes larger than the unit of truth: one part can succeed while another fails, yet the batch offers no honest state for that mixed result. Reviewability, retry, concurrency, and rollback all become ambiguous because the system no longer knows what the atomic promise is.

## Trigger When
Trigger when a task or tool call combines independent searches, edits, migrations, validations, or decisions merely for convenience, especially when they could proceed concurrently or be reviewed separately.

## Do Not Trigger When
Do not trigger when the steps jointly establish one invariant and partial completion would be meaningless or unsafe.

## Distinguish From
scope-creep adds unjustified work. serial-when-parallel executes independent work sequentially. This rule concerns an intent whose semantic boundary is already too broad before execution begins.

## Decision Procedure
1. List the observable outcomes requested.
2. Ask whether each can succeed or fail independently.
3. Split along those independent truth conditions.
4. Recompose only where a real invariant requires atomicity.

## Nudge
Make the unit of execution match the unit of truth. Split independent outcomes; keep only genuinely atomic work together.
