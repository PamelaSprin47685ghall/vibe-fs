# runtime-checked-builder — Enforcer

## Definition
A runtime-checked builder permits an object to exist in incomplete or contradictory construction states, then asks a final `build` or validation step to discover whether the preceding mutation sequence happened to satisfy the contract.

## Governing Principle
Construction is a proof that enough facts exist to create a value. A fluent mutable builder postpones that proof while exposing intermediate states that have no domain meaning. The API therefore accepts many sequences whose only purpose is to fail later. When required stages are encoded in types or gathered by one validated constructor, invalid construction paths disappear rather than becoming runtime branches.

## Trigger When
Trigger when required fields are filled through setters/fluent mutation and correctness is checked only at the end, especially when callers can forget stages, set fields in illegal combinations, or reuse partially built instances.

## Do Not Trigger When
Do not trigger when a builder cannot expose or produce an invalid object because its stages are statically encoded, or when one validated constructor atomically checks all truly dynamic constraints.

## Distinguish From
illegal-state-representable concerns invalid states of a completed domain value. This rule concerns invalid states during the act of constructing that value.

## Decision Procedure
Separate facts known at compile/construction time from genuinely dynamic validation. Make required structure impossible to omit; reserve runtime rejection for constraints whose truth cannot be known earlier.

## Nudge
Construction should prove readiness, not create a mutable puzzle and inspect it afterward. Encode required stages in the API or validate once in an atomic constructor.
