# type-erosion-at-boundary — Enforcer

## Definition
Type erosion occurs when dynamic, unchecked, reflective, or unboxed representations cross the adapter boundary and continue circulating inside code that should reason in domain types.

## Governing Principle
A boundary is where uncertainty should be spent. External data may arrive weakly typed, but once admitted inward the system should have paid the cost of parsing and validation and gained stronger propositions in return. Allowing `any`, unchecked casts, or reflective lookup to persist means every downstream use reopens the same uncertainty and can fail far from the point where evidence was available.

## Trigger When
Trigger when `any`, reflection, dynamic property access, unboxing, unchecked casts, or generic maps escape protocol/adapters into domain/application logic.

## Do Not Trigger When
Do not trigger when dynamic decoding is confined to the edge and produces validated domain values before crossing inward.

## Distinguish From
weak-boundary-parsing delays validation of external shape. primitive-obsession preserves weak identity despite static primitives. This rule specifically loses static type information through dynamic/unchecked representation.

## Decision Procedure
Locate the last point that possesses the raw external representation. Validate and translate there, then expose a type whose constructors encode the facts downstream code is entitled to assume.

## Nudge
Spend uncertainty once at the edge. Dynamic data may enter through an adapter, but only validated typed values should leave it for the domain.
