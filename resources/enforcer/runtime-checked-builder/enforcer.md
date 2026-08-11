# runtime-checked-builder — Enforcer

## Definition
A complex object is assembled through setters or fluent mutation and only validated after construction, so incomplete intermediate states are representable.

## Trigger When
A complex object is built through setters or fluent mutation and only validated after construction, allowing incomplete intermediate states.

## Do Not Trigger When
Do not fire when construction uses a single validated constructor, required-stage phantom types, or a builder that cannot produce an unvalidated instance.

## Distinguish From
illegal-state-representable is the broader type problem; this tip specifically targets post-hoc builder validation.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Construction correctness is deferred to runtime. Encode the required construction stages or use one validated constructor.

## Examples
### Positive
A complex object is built through setters or fluent mutation and only validated after construction, allowing incomplete intermediate states.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when construction uses a single validated constructor, required-stage phantom types, or a builder that cannot produce an unvalidated instance.
