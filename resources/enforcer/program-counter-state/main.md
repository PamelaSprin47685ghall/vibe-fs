# program-counter-state — Main

## What To Do Now
Remove fields whose primary purpose is to remember execution position. Express sequencing with local continuations/structured control flow and persist only states the domain genuinely recognizes.

## Why This Matters
Persisted program counters freeze an implementation strategy into the data model. Refactoring the control flow then becomes a data migration, while concurrency and recovery must interpret partially executed code as if it were business state.

## Repair Strategy
Separate real workflow facts from interpreter position. If the domain has named statuses, model them explicitly; otherwise keep step/next-action data within the lifetime of the operation that needs it.

## Wrong Fixes
Do not rename `currentStep` to `status` while preserving the same execution-pointer semantics. Domain language should reflect external meaning, not disguise implementation state.

## Verification
Change the internal sequencing structure conceptually. Durable state should not need to change unless the domain-visible workflow itself changed.

## Done When
Stored/shared state describes reality, while “where the code should continue” is owned by control flow rather than masquerading as a domain fact.
