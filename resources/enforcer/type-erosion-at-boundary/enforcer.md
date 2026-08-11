# type-erosion-at-boundary — Enforcer

## Definition
Type erosion occurs when dynamic, unchecked, reflective, or unboxed representations cross the adapter boundary and continue circulating inside code that should reason in domain types.

## Governing Principle
A boundary is where uncertainty should be spent. External data may arrive weakly typed, but once admitted inward the system should have paid the cost of parsing and validation and gained stronger propositions in return. The root-cause is spending that uncertainty after the adapter instead of at it: allowing `any`, unchecked casts, or reflective lookup to persist means every downstream use reopens the same uncertainty and can fail far from the point where evidence was available.

## Trigger When
Trigger when `any`, reflection, dynamic property access, unboxing, unchecked casts, or generic maps escape protocol/adapters into domain/application logic.

## Do Not Trigger When
- Dynamic decoding is confined to the edge and produces validated domain values before crossing inward.
- Reflection or `unknown` lives only inside a serializer owned by the adapter and is narrowed before return.
- Domain code uses closed typed unions that were constructed at ingress, even if the wire form was dynamic.
- Test fixtures build domain values through the same constructors production uses, not by casting maps.

## Distinguish From
`weak-boundary-parsing` delays validation of external shape. `primitive-obsession` preserves weak identity despite static primitives. Tie-break: if static type information is lost through dynamic/unchecked representation leaking inward, use this rule; if external shape is repeatedly interpreted without a strong model, use `weak-boundary-parsing`.

## Decision Procedure
Locate the last point that possesses the raw external representation. Validate and translate there, then expose a type whose constructors encode the facts downstream code is entitled to assume.

## Examples
- positive: an HTTP handler passes `req.body as any` into a domain service that reads fields dynamically.
- near-miss: the adapter decodes JSON to `unknown`, validates, and returns a `Money`/`UserId` domain value.
- counterexample: validated code still uses `string` for identifiers that need nominal types — that is `primitive-obsession`.

## Nudge
Spend uncertainty once at the edge. Dynamic data may enter through an adapter, but only validated typed values should leave it for the domain.
