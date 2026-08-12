# runtime-checked-builder — Enforcer

## Definition
A runtime-checked builder is defective when it exposes an object that is allowed to be incomplete or contradictory for an arbitrary sequence of calls, then asks a late `build()` / `validate()` step to discover whether the caller happened to perform the ritual correctly.

The root cause is **construction protocol encoded as mutable convention**. Facts that could have been required by the API are postponed into runtime state, so invalid call sequences become first-class possibilities.

## Governing Principle
Construction is not paperwork. It is the proof that enough facts exist to admit a value.

A public builder that starts invalid and gradually becomes maybe-valid creates a shadow lifecycle whose states exist only because the API was designed around setters. Callers can forget steps, repeat them, reorder them, reuse half-built instances, or observe contradictory intermediate combinations. The final `build()` error is not robustness; it is the API discovering a mistake it invited.

But not every runtime constructor check is a smell. Dynamic constraints—database uniqueness, numeric ranges from runtime values, cross-object facts—must be checked at runtime. The rule attacks **avoidable temporal construction state**, not validation itself.

## Trigger When
Trigger when:

- required fields are supplied through optional setter calls and `build()` throws/returns failure if one was forgotten;
- method order is documented in prose instead of represented by the API;
- the same builder instance can be reused after partial or failed construction;
- illegal combinations can be assembled and are rejected only at the end;
- callers defensively call `isValid()` before `build()`;
- tests enumerate “forgot to call setter X” scenarios that the type/API could have made impossible.

## Do Not Trigger When
- A staged/phantom-typed builder makes unavailable operations impossible at compile time.
- One atomic constructor takes all required data and returns a typed rejection for genuinely dynamic constraints.
- A private mutable accumulator cannot escape and is converted once into the real domain value.
- A parser incrementally consumes a stream because incremental state is the actual domain of parsing, not a ceremonial object-construction protocol.
- A complex UI form is intentionally incomplete user state and is not masquerading as the completed domain object.

## Distinguish From
`illegal-state-representable` concerns invalid **finished domain values**. `phase-flag-accumulation` concerns lifecycle encoded as proliferating flags. `clone-and-mutate-derived` starts from a completed value and mutates a derived copy.

Tie-break: if the defect is “caller can perform an invalid construction sequence before `build`,” use this rule. If the built value itself can still be contradictory, use `illegal-state-representable` too.

## Decision Procedure
Separate three things:

1. facts known when construction begins;
2. facts that become available through legitimate staged work;
3. constraints that are inherently dynamic.

Make (1) constructor inputs. If (2) is a real semantic protocol, encode stages in types or explicit states; if it is merely setter ceremony, remove it. Keep runtime rejection only for (3).

Ask the killer question: **what useful domain meaning does the incomplete public builder have?** If the answer is “none; it only exists until callers remember all the setters,” the state should not be public.

## Examples
- positive: `new OrderBuilder().withItem(x).build()` fails because `customer` was never set, and the same half-built object remains reusable.
- positive: a config builder has twelve optional setters, three required combinations, and a 200-line `validate()` that reconstructs the legal state space at the end.
- near-miss: `Order.create(customer, items)` returns `Result<Order, ValidationError>` because total/credit rules depend on runtime values.
- near-miss: `Builder<NeedsCustomer>` exposes `withCustomer` and returns `Builder<Ready>`; only `Ready` has `build`.
- counterexample: a parser state machine is incremental because input itself arrives incrementally; those intermediate states have real semantics.

## Nudge
If the only purpose of an incomplete object is to wait for the caller to remember the rest of the ritual, the object should not exist.

Require what is already knowable. Encode real stages. Validate only what reality forces you to learn at runtime.
