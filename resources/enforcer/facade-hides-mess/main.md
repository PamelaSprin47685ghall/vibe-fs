# facade-hides-mess — Main

## What To Do Now
Remove the facade as the supposed cure and repair the ownership, dependency, and state boundaries it currently conceals. Reintroduce a narrow facade only if a genuine subsystem contract remains useful afterward.

## Why This Matters
A facade can reduce caller knowledge only when the hidden system has a coherent internal model. Over disorder, it reduces visibility without reducing coupling. That makes the architecture harder to diagnose while leaving every underlying reason for change intact.

## Repair Strategy
Map the actual owners and dependency edges beneath the wrapper. Eliminate duplicate authority and illicit cross-boundary knowledge, then decide whether one stable public entry point naturally follows from the repaired structure.

## Wrong Fixes
Do not add more wrapper methods, adapters, or “service” layers to make the surface look uniform. Uniform forwarding is not a model.

## Verification
Internal components should have clear owners and acyclic, intentional dependencies even if the facade is temporarily removed from the diagram.

## Done When
The facade, if retained, hides coherent implementation detail rather than architectural debt, and deleting it would reveal complexity—not contradiction.
