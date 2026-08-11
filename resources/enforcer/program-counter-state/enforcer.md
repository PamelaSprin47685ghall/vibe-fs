# program-counter-state — Enforcer

## Definition
Program-counter state appears when fields such as stage, phase, next action, current step, lease, generation, or owner primarily encode where execution should resume rather than a durable real-world fact. The root-cause is that interpreter position is persisted or shared as if it were a domain fact, freezing implementation sequencing into authoritative state.

## Governing Principle
Control state and domain state answer different questions. Domain state describes the world; a program counter describes the interpreter's current location. Persisting or sharing the latter turns transient implementation sequencing into business data, making restarts and concurrent observers reason about internal continuations that should have remained local to structured control flow.

## Trigger When
Trigger when mutable fields are repeatedly inspected to decide "what code runs next" and have little independent domain meaning outside that control decision.

## Do Not Trigger When
- The stage/lease/generation is itself a durable domain fact meaningful to external actors—for example a workflow status explicitly promised by the product.
- The field is local to one in-flight operation and never stored or shared as authoritative state.
- The value is a real-world attribute (owner, amount, location) that happens to be read by control flow.

## Distinguish From
phase-flag-accumulation encodes lifecycle through flag products. implicit-control-flow hides sequencing. Tie-break: if the interpreter's position is reified as shared/persisted state, this rule; if lifecycle is a boolean product, phase-flag-accumulation; if sequencing is hidden in callbacks/globals, implicit-control-flow.

## Decision Procedure
Ask whether an external domain observer would care about this field if the implementation used a different control structure. If not, keep it local to control flow rather than authoritative state.

## Examples
- positive: A document stores `currentStep = "callValidateThenSave"` so crash recovery resumes that function.
- near-miss: The product exposes `Draft | Submitted | Approved` as a customer-visible workflow status.
- counterexample: Sequencing lives in a local function; durable state is only domain facts such as `approvedAt`.

## Nudge
Do not persist the instruction pointer unless the domain itself owns a workflow state. Keep implementation sequencing in structured control flow and durable state about the world.
