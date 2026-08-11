# manual-toil-repeat — Enforcer

## Definition
A repeated mechanical procedure is performed manually again despite being deterministic and suitable for a script, generator, or reusable skill.

## Trigger When
A repeated mechanical procedure is performed manually again despite being deterministic and suitable for a script, generator, or reusable skill.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
leftover-scaffolding, missing-architecture-gate, incidental-complexity-dominates

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Repeated mechanical work remains manual. Automate it and preserve the procedure as a maintained tool.

## Examples
### Positive
A repeated mechanical procedure is performed manually again despite being deterministic and suitable for a script, generator, or reusable skill.

### Near miss
Looks related to leftover-scaffolding but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
