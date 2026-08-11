# translator-layer-bloat — Enforcer

## Definition
Translator, broker, governor, coordinator, manager, adapter, or mediator layers merely forward calls without enforcing a real boundary or transformation.

## Trigger When
Translator, broker, governor, coordinator, manager, adapter, or mediator layers merely forward calls without enforcing a real boundary or transformation.

## Do Not Trigger When
Do not fire when the layer enforces auth, validation, protocol mapping, batching, or another real invariant not present on either side.

## Distinguish From
facade-hides-mess conceals a tangle; generic-helper-bucket is grab-bag utils; this tip is pure forwarding ceremony.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A forwarding layer adds ceremony without a concept. Remove it or give it a genuine invariant and ownership boundary.

## Examples
### Positive
Translator, broker, governor, coordinator, manager, adapter, or mediator layers merely forward calls without enforcing a real boundary or transformation.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when the layer enforces auth, validation, protocol mapping, batching, or another real invariant not present on either side.
