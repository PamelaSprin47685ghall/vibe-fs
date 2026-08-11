# program-counter-state — Enforcer

## Definition
Program-counter state appears when fields such as stage, phase, next action, current step, lease, generation, or owner primarily encode where execution should resume rather than a durable real-world fact.

## Governing Principle
Control state and domain state answer different questions. Domain state describes the world; a program counter describes the interpreter’s current location. Persisting or sharing the latter turns transient implementation sequencing into business data, making restarts and concurrent observers reason about internal continuations that should have remained local to structured control flow.

## Trigger When
Trigger when mutable fields are repeatedly inspected to decide “what code runs next” and have little independent domain meaning outside that control decision.

## Do Not Trigger When
Do not trigger when the stage/lease/generation is itself a durable domain fact meaningful to external actors—for example a workflow status explicitly promised by the product.

## Distinguish From
phase-flag-accumulation encodes lifecycle through flag products. implicit-control-flow hides sequencing. This rule reifies the interpreter’s position as shared state.

## Decision Procedure
Ask whether an external domain observer would care about this field if the implementation used a different control structure. If not, keep it local to control flow rather than authoritative state.

## Nudge
Do not persist the instruction pointer unless the domain itself owns a workflow state. Keep implementation sequencing in structured control flow and durable state about the world.
