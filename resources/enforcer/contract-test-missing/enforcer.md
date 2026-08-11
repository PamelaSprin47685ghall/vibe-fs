# contract-test-missing — Enforcer

## Definition
A Host, provider, storage, process, network, plugin, or language boundary is changed without a contract-level test.

## Trigger When
A Host, provider, storage, process, network, plugin, or language boundary is changed without a contract-level test.

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
A boundary contract changed without a contract test. Verify the exact input, output, identity, and failure semantics.

## Examples
### Positive
A Host, provider, storage, process, network, plugin, or language boundary is changed without a contract-level test.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
A boundary contract changed without a contract test. Verify the exact input, output, identity, and failure semantics.
