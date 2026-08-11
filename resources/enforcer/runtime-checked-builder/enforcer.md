# runtime-checked-builder — Enforcer

## Definition
A runtime-checked builder lets an incomplete or contradictory object exist, then asks a late `build` or validation step to discover whether the preceding mutation sequence happened to satisfy the contract. The root-cause is that construction postpones the proof that required facts exist, so illegal sequences become representable worlds whose only purpose is to fail later.

## Governing Principle
Construction is a proof that enough facts exist to create a value. A fluent mutable builder delays that proof while exposing intermediate states that have no domain meaning. When required stages are encoded in types or gathered by one validated constructor, invalid construction paths disappear rather than becoming runtime branches.

## Trigger When
Trigger when required fields are filled through setters or fluent mutation and correctness is checked only at the end, especially when callers can omit stages, set illegal combinations, or reuse a half-built instance.

## Do Not Trigger When
- Stages are statically encoded so the API cannot expose or produce an invalid object.
- One validated constructor atomically checks truly dynamic constraints and never yields an incomplete instance.
- Optional fields have documented defaults and required data is already constructor parameters.
- The mutable object is an internal accumulator that never escapes and is converted by a single checked constructor.

## Distinguish From
`illegal-state-representable` concerns invalid states of a completed domain value. `boolean-blindness` hides named modes in flags that construction may also accumulate. `clone-and-mutate-derived` copies a finished value then mutates it. This rule concerns invalid states during construction itself. Tie-break: if the incomplete or contradictory object exists before `build`, this rule owns the case.

## Decision Procedure
1. Separate facts knowable at construction time from genuinely dynamic constraints.
2. Make required structure impossible to omit.
3. Reserve runtime rejection for constraints whose truth cannot be known earlier.
4. Confine any accumulator so it cannot masquerade as the domain value.

## Examples
- positive: `new OrderBuilder().setItem(x).build()` throws because `customer` was never set, and the half-built instance can be reused.
- near-miss: a phantom-typed builder where `withCustomer` returns `Builder<HasCustomer>` and `build` exists only on the complete type.
- counterexample: `Order.create(customer, items)` validates dynamic totals once and never exposes an incomplete order.

## Nudge
Construction should prove readiness, not create a mutable puzzle and inspect it afterward. Encode required stages in the API or validate once in an atomic constructor.
