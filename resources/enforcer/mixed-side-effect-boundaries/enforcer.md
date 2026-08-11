# mixed-side-effect-boundaries — Enforcer

## Definition
A single function or module simultaneously owns unrelated effects such as storage, network, process control, UI, Git, and policy decisions.

## Trigger When
A single function or module simultaneously owns unrelated effects such as storage, network, process control, UI, Git, and policy decisions.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
god-module, impure-core, mock-hidden-state

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Unrelated side-effect boundaries are mixed together. Isolate each effect behind a narrow port and keep policy pure.

## Examples
### Positive
A single function or module simultaneously owns unrelated effects such as storage, network, process control, UI, Git, and policy decisions.

### Near miss
Looks related to god-module but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
