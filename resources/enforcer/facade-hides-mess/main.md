# facade-hides-mess — Main

## What To Do Now
Remove the facade as the supposed cure and repair the ownership, dependency, and state boundaries it currently conceals. Reintroduce a narrow facade only if a genuine subsystem contract remains useful afterward.

## Why This Matters
A facade can reduce caller knowledge only when the hidden system has a coherent internal model. Over disorder, it reduces visibility without reducing coupling. That makes the architecture harder to diagnose while leaving every underlying reason for change intact.

## Repair Strategy
Map the actual owners and dependency edges beneath the wrapper. Eliminate duplicate authority and illicit cross-boundary knowledge, then decide whether one stable public entry point naturally follows from the repaired structure.

## Decision Branches
- If the wrapper forwards into unchanged violations, strip it as the “fix” and repair the graph beneath.
- If internals become coherent, a narrow facade may remain as the real boundary.
- If the pain is extra forwarding with no mess beneath, use `translator-layer-bloat`.

## Common Wrong Fixes
- Do not add more wrapper methods, adapters, or “service” layers to make the surface look uniform.
- Do not keep the facade and “clean internals later.”
- Do not rename the tangled module and call the rename a boundary.
- Do not hide cycles or dual ownership behind DI registration in the facade.

## Verification
Internal components should have clear owners and acyclic, intentional dependencies even if the facade is temporarily removed from the diagram. The invariant is that a retained facade hides coherent implementation detail, not architectural contradiction.

## Done When
The facade, if retained, hides coherent implementation detail rather than architectural debt, and deleting it would reveal complexity—not contradiction.
