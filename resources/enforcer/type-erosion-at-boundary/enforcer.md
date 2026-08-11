# type-erosion-at-boundary — Enforcer

## Definition
`any`, unchecked casts, reflection, dynamic property access, or unboxing escape the designated adapter boundary and enter domain logic.

## Trigger When
`any`, unchecked casts, reflection, dynamic property access, or unboxing escape the designated adapter boundary and enter domain logic.

## Do Not Trigger When
Do not fire when dynamic decoding is confined to adapters that emit validated domain types before crossing inward.

## Distinguish From
weak-boundary-parsing leaves data weakly typed; primitive-obsession overuses primitives; this tip is dynamic/unchecked types leaking past the adapter.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Type information is being discarded beyond the adapter boundary. Contain dynamic decoding and expose a typed contract.

## Examples
### Positive
`any`, unchecked casts, reflection, dynamic property access, or unboxing escape the designated adapter boundary and enter domain logic.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when dynamic decoding is confined to adapters that emit validated domain types before crossing inward.
