# runtime-checked-builder — Enforcer

## Definition
A runtime-checked builder permits an object to exist in incomplete or contradictory construction states, then asks a final `build` or validation step to discover whether the preceding mutation sequence happened to satisfy the contract.

## Governing Principle
Construction is a proof that enough facts exist to create a value. A fluent mutable builder postpones that proof while exposing intermediate states that have no domain meaning. The API therefore accepts many sequences whose only purpose is to fail later. When required stages are encoded in types or gathered by one validated constructor, invalid construction paths disappear rather than becoming runtime branches.

## Trigger When
Trigger when required fields are filled through setters/fluent mutation and correctness is checked only at the end, especially when callers can forget stages, set fields in illegal combinations, or reuse partially built instances.

## Do Not Trigger When
- A builder cannot expose or produce an invalid object because its stages are statically encoded.
- One validated constructor atomically checks all truly dynamic constraints and never yields an incomplete instance.
- Optional fields have documented defaults and required data is already constructor parameters.
- The mutable object is an internal accumulator that never escapes and is converted by a single checked constructor.

## Distinguish From
illegal-state-representable concerns invalid states of a completed domain value. boolean-blindness hides meaning in flags that construction may also accumulate. This rule concerns invalid states during the act of constructing that value. Tie-break: fire here when the incomplete/contradictory object exists before `build`; fire illegal-state-representable when a supposedly finished domain value can still be invalid; fire boolean-blindness when the construction API is already staged but uses opaque booleans for required meaning.

## Decision Procedure
Separate facts known at compile/construction time from genuinely dynamic validation. Make required structure impossible to omit; reserve runtime rejection for constraints whose truth cannot be known earlier.

## Examples
- positive: `new OrderBuilder().setItem(x).build()` throws because `customer` was never set, and the half-built instance can be reused.
- near-miss: a phantom-typed builder where `withCustomer` returns `Builder<HasCustomer>` and `build` exists only on the complete type.
- counterexample: `Order.create(customer, items)` validates dynamic totals once and never exposes an incomplete order.

## Nudge
Construction should prove readiness, not create a mutable puzzle and inspect it afterward. Encode required stages in the API or validate once in an atomic constructor.
