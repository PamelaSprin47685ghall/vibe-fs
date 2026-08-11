# ephemeral-verification — Enforcer

## Definition
A one-off shell command, temporary script, manual probe, or debug print is the only proof and is not converted into a durable test or gate.

## Trigger When
A one-off shell command, temporary script, manual probe, or debug print is the only proof and is not converted into a durable test or gate.

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
Verification exists only as an ephemeral probe. Preserve it as a repeatable test, script, or canary.

## Examples
### Positive
A one-off shell command, temporary script, manual probe, or debug print is the only proof and is not converted into a durable test or gate.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
Verification exists only as an ephemeral probe. Preserve it as a repeatable test, script, or canary.
