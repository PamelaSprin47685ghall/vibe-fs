# wrong-rule-composition — Main

## What To Do Now
Classify rules as dependent or independent. Use applicative/accumulating composition for independents and monadic/short-circuit for dependents.

## Repair Strategy
Refactor validation pipelines. Stop reporting cascade noise after a missing prerequisite. Gather all field-level errors that do not depend on each other.

## Decision Branches
If a UX needs partial dependent hints, compute them separately from hard gate composition. If performance requires fail-fast everywhere, document that product choice.

## Wrong Fixes
Always fail-fast on multi-field forms. Always accumulating after a null parent access that makes child errors nonsense. Mixing both randomly per call site.

## Verification
Dependent failure yields one root error without cascade junk; independent invalid fields all appear together.

## Done When
Composition matches dependency structure; error sets are meaningful and complete.

## Scope and Authority
Validation and business rule pipelines.
