# incidental-complexity-dominates — Main

## What To Do Now
Identify machinery introduced by the solution rather than demanded by the domain, then remove or compress it until essential concepts become the main visible structure.

## Why This Matters
Reader attention is the scarce resource architecture allocates. Every wrapper, lifecycle rule, configuration key, and translation step consumes that resource before any business reasoning begins. When accidental mechanisms dominate, even simple changes require global context.

## Repair Strategy
Trace one representative domain operation end to end. Collapse pass-through layers, use language/platform primitives directly, remove duplicate representations, and keep only machinery that protects a real invariant or external constraint.

## Wrong Fixes
Do not merely shorten files or hide ceremony behind generators. Generated or encapsulated complexity still exists if maintainers must understand it to change behavior safely.

## Verification
A reader should be able to explain the domain operation mostly in domain terms, with infrastructure concepts appearing only where reality forces a boundary.

## Done When
The code’s conceptual mass is proportional to the problem’s irreducible complexity rather than to frameworks, glue, and historical accidents of implementation.
