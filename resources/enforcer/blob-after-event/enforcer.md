# blob-after-event — Enforcer

## Definition
A journal event referencing large content is appended before the referenced blob is durably written.

## Trigger When
A journal event referencing large content is appended before the referenced blob is durably written.

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
A durable event can point to missing content. Write and verify the blob before appending the reference.

## Examples
### Positive
A journal event referencing large content is appended before the referenced blob is durably written.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
A durable event can point to missing content. Write and verify the blob before appending the reference.
