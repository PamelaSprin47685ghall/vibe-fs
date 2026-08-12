# runtime-checked-builder — Main

## What To Do Now
Collapse ceremonial mutable construction into one atomic constructor or a staged API whose types expose only legitimate next operations. Required facts that are already knowable should be required up front; genuinely dynamic constraints may remain runtime validation.

The owner is the construction boundary. A late `validate()` should not be compensating for an API that deliberately manufactured invalid intermediate states.

## Why This Matters
A builder that can be “not ready yet” without that incompleteness having domain meaning creates temporary worlds the rest of the program must guard against. The API turns omissions into runtime events:

- forgotten setter;
- duplicated setter;
- invalid ordering;
- reuse after failed build;
- contradictory fields accumulated before validation.

Tests then grow around mistakes the type surface itself invited.

The right goal is not “zero runtime validation.” That would be fantasy. The goal is to reserve runtime checks for facts that **cannot be known earlier**, while removing procedural rituals whose only purpose is to assemble already-known required data.

## Repair Strategy
1. Mark every builder field as required, optional-with-default, stage-dependent, or dynamically validated.
2. Put required data into constructor/function parameters.
3. If stages represent real semantic phases, encode them as explicit states/types with only legal transitions.
4. Keep optional configuration truly optional and supply explicit defaults.
5. Keep dynamic validation in one constructor/result boundary.
6. Make any mutable accumulator private and non-escaping.
7. Delete “call these methods in this order” documentation once the API makes bad order impossible.

## Decision Branches
- If all required data is available at one call site, prefer one constructor/function.
- If different facts become available at real semantic stages, use staged types or explicit state rather than a mutable maybe-valid bag.
- If the constraint depends on runtime data, return a typed validation failure; do not contort the type system into pretending the fact was statically knowable.
- If the intermediate object is itself meaningful business state (for example a draft form), model it honestly as that state rather than calling it a half-built final object.

## Common Wrong Fixes
- Add more `isValid` checks to the same builder.
- Throw earlier from each setter while keeping the same invalid public state space.
- Freeze the object after `build()` but still allow arbitrary incomplete instances before it.
- Encode method ordering only in docs/comments.
- Use a phantom/staged type so complicated that understanding construction costs more than the impossible states it removes. Types must buy clarity, not ceremony.

## Verification
Try to omit required facts, call operations in the wrong order, repeat incompatible stages, and reuse failed construction state. The public API should make those mistakes impossible or reject them at the single honest dynamic boundary before a domain value escapes.

Also test a genuinely dynamic invalid value to prove runtime validation still exists where reality requires it.

Invariant: **no escaped object is incomplete merely because the caller has not yet finished an API ritual.**

## Done When
Required construction facts are explicit, real stages are modeled as real stages, dynamic validation remains where necessary, and callers no longer need procedural memory to create a valid value.