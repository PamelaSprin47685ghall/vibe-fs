# weak-boundary-parsing — Enforcer

## Definition
Untrusted or cross-language data remains weakly typed after entering the system, forcing downstream code to repeatedly infer its shape.

## Trigger When
Untrusted or cross-language data remains weakly typed after entering the system, forcing downstream code to repeatedly infer its shape.

## Do Not Trigger When
Do not fire when data is parsed and validated once at the boundary into strong internal types before use.

## Distinguish From
type-erosion-at-boundary leaks dynamics inward; stringly-typed-error is error prose; this tip is delayed/repeated shape inference on ingress data.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Boundary data was not normalized early enough. Parse and validate it once into a strong internal type.

## Examples
### Positive
Untrusted or cross-language data remains weakly typed after entering the system, forcing downstream code to repeatedly infer its shape.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when data is parsed and validated once at the boundary into strong internal types before use.
