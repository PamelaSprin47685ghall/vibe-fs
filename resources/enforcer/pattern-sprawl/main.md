# pattern-sprawl — Main

## What To Do Now
Collapse pattern scaffolding into the language feature that directly represents its semantic job: closed cases, pattern matching, first-class functions, modules, or immutable constructors.

## Why This Matters
Indirection is useful when it buys a capability the language lacks. Once that capability is native, the same indirection becomes translation overhead: readers must map factories back to choices, strategies back to functions, visitors back to case analysis.

## Repair Strategy
Identify whether variability is closed data or replaceable behavior. Model closed alternatives explicitly and exhaustively; pass behavior as functions; compose small modules where ownership matters. Preserve open interfaces only where third-party/open-world extension is real.

## Wrong Fixes
Do not mechanically convert every class to a union or every interface to a function. Simplification follows the variation model, not an anti-pattern slogan.

## Verification
The rewritten code should expose the same domain distinctions with fewer concepts and stronger exhaustiveness/typing guarantees.

## Done When
Every remaining design pattern earns its indirection through a real extensibility or lifecycle requirement rather than historical habit.
