# program-counter-state — Enforcer

## Definition
Stage, phase, lease, generation, next-action, current-step, owner, or equivalent fields encode where the program should execute next rather than a real-world fact.

## Trigger When
Stage, phase, lease, generation, next-action, current-step, owner, or equivalent fields encode where the program should execute next rather than a real-world fact.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
phase-flag-accumulation, implicit-control-flow, non-exhaustive-transition

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Control flow has been reified as mutable program-counter state. Replace it with structured control flow and local continuations.

## Examples
### Positive
Stage, phase, lease, generation, next-action, current-step, owner, or equivalent fields encode where the program should execute next rather than a real-world fact.

### Near miss
Looks related to phase-flag-accumulation but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
