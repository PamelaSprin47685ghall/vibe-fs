# serial-investigation — Enforcer

## Definition
Independent searches, file reads, source inspections, or diagnostics run one-by-one despite having no ordering dependency.

## Trigger When
Independent searches, file reads, source inspections, or diagnostics are performed one by one despite having no dependency.

## Do Not Trigger When
Do not fire when each step truly needs the prior result to choose the next query, or when tool limits force serialization.

## Distinguish From
serial-when-parallel is the general concurrency smell for any independent work; this tip focuses on investigation/read fan-out.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Independent investigation is unnecessarily serial. Run the reads and searches concurrently, then synthesize the evidence.

## Examples
### Positive
Independent searches, file reads, source inspections, or diagnostics are performed one by one despite having no dependency.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when each step truly needs the prior result to choose the next query, or when tool limits force serialization.
