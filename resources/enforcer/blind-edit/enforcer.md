# blind-edit — Enforcer

## Definition
Code is changed before locating the true owner, reading surrounding contracts, or understanding the affected call path.

## Trigger When
Code is changed before locating the true owner, reading surrounding contracts, or understanding the affected call path.

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
The implementation was edited before the governing context was understood. Read the owner and boundary contracts first.

## Examples
### Positive
Code is changed before locating the true owner, reading surrounding contracts, or understanding the affected call path.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
The implementation was edited before the governing context was understood. Read the owner and boundary contracts first.
