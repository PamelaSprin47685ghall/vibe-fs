# stale-documentation — Main

## What To Do Now
Update the owning spec, schema, examples, and diagrams in the same change as the behavior. Delete or relocate obsolete statements.

## Repair Strategy
Diff behavior against docs. Patch the authoritative plane first or together. Link the change notes to the doc update.

## Decision Branches
If docs live in another package, land a coordinated change. If a doc is superseded, mark it and point to the new authority rather than leaving both live.

## Wrong Fixes
Shipping code first and promising a doc follow-up that never lands. Updating a blog but not the schema contract. Leaving examples that fail against the new API.

## Verification
Follow docs-only instructions against the new code; they work. No contradictory authoritative page remains.

## Done When
Authoritative documentation matches the shipped contract in the same delivery.

## Scope and Authority
Owning specs, schemas, and official examples. Not every chat transcript.
