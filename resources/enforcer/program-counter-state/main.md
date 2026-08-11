# program-counter-state — Main

## What To Do Now
Remove fields whose primary purpose is to remember execution position. Express sequencing with local continuations/structured control flow and persist only states the domain genuinely recognizes. Structured control flow of the in-flight operation is who owns sequencing; durable domain facts are who owns the persistence invariant that stored state describes the world, not the instruction pointer.

## Why This Matters
Persisted program counters freeze an implementation strategy into the data model. Refactoring the control flow then becomes a data migration, while concurrency and recovery must interpret partially executed code as if it were business state.

## Repair Strategy
Separate real workflow facts from interpreter position. If the domain has named statuses, model them explicitly; otherwise keep step/next-action data within the lifetime of the operation that needs it.

## Decision Branches
- If an external observer would not care about the field under a different control structure, stop persisting it and keep sequencing local.
- If the product promises a workflow status, model that status as domain state—not as `currentStep` of the implementation.

## Common Wrong Fixes
- Rename `currentStep` to `status` while preserving execution-pointer semantics.
- Persist more step fields so recovery can jump into the middle of a function.
- Encode the next function name in the database.

## Verification
Change the internal sequencing structure conceptually. Durable state should not need to change unless the domain-visible workflow itself changed. The invariant is that stored/shared state describes reality, not the instruction pointer.

## Done When
Stored/shared state describes reality, while "where the code should continue" is owned by control flow rather than masquerading as a domain fact.
