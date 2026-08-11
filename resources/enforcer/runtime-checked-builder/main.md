# runtime-checked-builder — Main

## What To Do Now
Replace post-hoc mutable construction with one validated constructor or a staged API whose type makes required steps explicit. The constructor or staged type that admits the domain value is who owns the construction invariant that no escaped object is incomplete or contradictory.

## Why This Matters
A builder that can be incomplete enlarges the program with temporary worlds nobody wants. Callers need runtime checks, reuse becomes hazardous, and error handling grows around omissions the API itself invited. Strong construction narrows the state space before a domain value ever exists.

## Repair Strategy
Collect mandatory data in constructor parameters or encode stages so each method returns the next valid construction type. Keep dynamic business validation as a typed constructor result rather than as an exception at the end of a setter chain. Confine any internal accumulator so it cannot leak as the domain value.

## Decision Branches
- If required fields are known statically, encode them as constructor arguments or staged types so omission is unrepresentable.
- If some constraints are truly dynamic, keep one atomic constructor or result type and do not expose a mutable incomplete object.
- If an internal accumulator is unavoidable, confine it so it cannot masquerade as the domain value.

## Common Wrong Fixes
- Do not add more `isValid` checks to the same mutable builder.
- Do not throw from every setter while still allowing `build` on a reused instance.
- Do not freeze the object after `build` but leave incomplete instances publicly constructible.
- Do not document “call these setters in order” instead of making order and type the API.

## Verification
Attempt to omit or reorder required stages and construct contradictory combinations. Static structure or the single constructor boundary should reject them before an invalid domain instance escapes. The invariant is that no escaped value is incomplete or contradictory relative to the construction contract.

## Done When
Every produced object is valid by construction, and incomplete builder state is either unrepresentable or confined to an internal scope that cannot masquerade as the domain value.
