# pattern-sprawl — Main

## What To Do Now
Collapse pattern scaffolding into the language feature that directly represents its semantic job: closed cases, pattern matching, first-class functions, modules, or immutable constructors. The host language's native form for the same law (closed data, match, functions, modules) is who owns the abstraction invariant that remaining indirection still buys a capability the language lacks.

## Why This Matters
Indirection is useful when it buys a capability the language lacks. Once that capability is native, the same indirection becomes translation overhead: readers must map factories back to choices, strategies back to functions, visitors back to case analysis.

## Repair Strategy
Identify whether variability is closed data or replaceable behavior. Model closed alternatives explicitly and exhaustively; pass behavior as functions; compose small modules where ownership matters. Preserve open interfaces only where third-party/open-world extension is real.

## Decision Branches
- If variability is a closed set of cases, replace the hierarchy with data and exhaustive match.
- If variability is behavior or open-world extension, pass a function or keep a real plugin boundary—do not collapse those.

## Common Wrong Fixes
- Mechanically convert every class to a union or every interface to a function.
- Replace one pattern with another equally ceremonial one (abstract factory → builder).
- Keep the old hierarchy "for flexibility" after the native form already expresses the law.

## Verification
The rewritten code should expose the same domain distinctions with fewer concepts and stronger exhaustiveness/typing guarantees. The invariant is that remaining indirection still buys a capability the language lacks.

## Done When
Every remaining design pattern earns its indirection through a real extensibility or lifecycle requirement rather than historical habit.
