# callback-pyramid — Enforcer

## Definition
Nested callbacks or promise chains obscure resource scope, cancellation, error propagation, or the linear story of the operation.

## Trigger When
Nested callbacks or promise chains obscure resource scope, cancellation, error propagation, or the linear story of the operation.

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
Nested continuations are obscuring the operation. Flatten the flow with structured async control and scoped resources.

## Examples
### Positive
Nested callbacks or promise chains obscure resource scope, cancellation, error propagation, or the linear story of the operation.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
Nested continuations are obscuring the operation. Flatten the flow with structured async control and scoped resources.
