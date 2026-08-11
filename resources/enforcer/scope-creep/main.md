# scope-creep — Main

## What To Do Now
Cut the diff back to the task contract. Park unrelated cleanup and redesigns as separate work with their own acceptance.

## Repair Strategy
Re-read the task acceptance criteria. Revert or split out opportunistic edits. Keep only dependency-forced touch points.

## Decision Branches
If a blocker forces a small adjacent fix, keep it minimal and name it. If architecture work is truly required, renegotiate scope explicitly before continuing.

## Wrong Fixes
Drive-by renames across the repo. "While I am here" refactors that obscure the review. Mixing feature work with unrelated dependency upgrades.

## Verification
Every changed file maps to the stated acceptance criteria or a forced compile/migration edge.

## Done When
The delivery matches the justified scope; unrelated work is split out or dropped.

## Scope and Authority
Task and PR boundaries. Not multi-step planned migrations that are the task itself.
