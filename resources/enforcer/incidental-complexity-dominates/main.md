# incidental-complexity-dominates — Main

## What To Do Now
Repair at the root-cause owner of the domain operation: who owns that end-to-end path must collapse solution-imposed machinery until essential concepts dominate. Do not add another wrapper around the ceremony.

## Why This Matters
Reader attention is the scarce resource architecture allocates. Every wrapper, lifecycle rule, configuration key, and translation step consumes that resource before any business reasoning begins. When accidental mechanisms dominate, even simple changes require global context.

## Repair Strategy
Trace one representative domain operation end to end. Collapse pass-through layers, use language/platform primitives directly, remove duplicate representations, and keep only machinery that protects a real invariant or external constraint.

## Decision Branches
- If a layer does not protect an invariant or external constraint, collapse it.
- If complexity is demanded by a real protocol or domain state, keep it and make that necessity visible.

## Common Wrong Fixes
- Do not merely shorten files or hide ceremony behind generators. Generated or encapsulated complexity still exists if maintainers must understand it to change behavior safely.
- Do not add another wrapper to “simplify” call sites while increasing the stack.
- Do not relocate ceremony into a shared framework module that every change still must learn.

## Verification
A reader should be able to explain the domain operation mostly in domain terms, with infrastructure concepts appearing only where reality forces a boundary. The invariant: conceptual mass is proportional to irreducible problem complexity, not to glue.

## Done When
The code’s conceptual mass is proportional to the problem’s irreducible complexity rather than to frameworks, glue, and historical accidents of implementation.
