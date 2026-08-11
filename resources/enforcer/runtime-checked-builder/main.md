# runtime-checked-builder — Main

## What To Do Now
Replace post-hoc mutable construction with one validated constructor or a staged API whose type makes required construction steps explicit.

## Why This Matters
A builder that can be incomplete enlarges the program with temporary worlds nobody wants. Callers need runtime checks, reuse becomes hazardous, and error handling grows around omissions the API itself invited. Strong construction narrows the state space before a domain value ever exists.

## Repair Strategy
Collect mandatory data in constructor parameters or encode stages so each method returns the next valid construction type. Keep dynamic business validation as a typed constructor result rather than as an exception at the end of a setter chain.

## Wrong Fixes
Do not add more `isValid` checks to the same mutable builder. Detection after the illegal intermediate exists is weaker than removing the construction path.

## Verification
Attempt to omit or reorder required stages and construct contradictory combinations. Static structure or the single constructor boundary should reject them before an invalid domain instance escapes.

## Done When
Every produced object is valid by construction, and incomplete builder state is either unrepresentable or confined to an internal scope that cannot masquerade as the domain value.
