# destructive-without-authorization — Enforcer

## Definition
Data, files, branches, worktrees, resources, or external state are deleted or overwritten without explicit authority and a verified target.

## Trigger When
Data, files, branches, worktrees, resources, or external state are deleted or overwritten without explicit authority and a verified target.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when the observed pattern is intentional, documented, and verified at the owning contract.

## Distinguish From
Related tips that share vocabulary but different boundary.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A destructive action lacks explicit authorization or target verification. Stop and establish both before proceeding.

## Examples
### Positive
Data, files, branches, worktrees, resources, or external state are deleted or overwritten without explicit authority and a verified target.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
A destructive action lacks explicit authorization or target verification. Stop and establish both before proceeding.
