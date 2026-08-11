# cancellation-not-propagated — Enforcer

## Definition
A cancellation token or abort signal stops at an outer layer while inner network, process, tool, or child work continues.

## Trigger When
A cancellation token or abort signal stops at an outer layer while inner network, process, tool, or child work continues.

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
Cancellation does not reach owned work. Propagate the cancellation signal through every resource boundary.

## Examples
### Positive
A cancellation token or abort signal stops at an outer layer while inner network, process, tool, or child work continues.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
Cancellation does not reach owned work. Propagate the cancellation signal through every resource boundary.
