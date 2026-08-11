# framework-tax — Main

## What To Do Now
Strip away framework mechanisms that do not carry real domain or operational value and expose the underlying operation through the simplest native construct that fits. The domain operation is who owns the design’s dominant structure; container, hook, and config machinery own only the complexity they actually remove.

## Why This Matters
Every framework concept consumes reader attention before business reasoning begins. When that cost is not repaid by eliminated complexity, the architecture becomes a tutorial for the framework rather than a model of the system.

## Repair Strategy
Identify what the framework is actually buying—lifecycle, discovery, interception, resource management, portability. Retain only benefits the product needs, replacing the rest with direct functions, modules, language features, or small explicit boundaries.

## Decision Branches
- If ceremony exceeds the domain operation and buys little, remove the ritual and call the operation directly.
- If the framework owns real cross-cutting risk (auth, transactions, resource lifetime), keep that slice and drop the rest.
- If the mistake was importing the ecosystem at all, unwind under `dependency-bloat`.

## Common Wrong Fixes
- Do not build a custom micro-framework that recreates the same ceremony under local names.
- Do not add another layer of “simplifying” annotations on top of the container.
- Do not keep unused lifecycle hooks “for consistency.”
- Do not replace config files with equivalent code generation that still dominates the operation.

## Verification
A reader should be able to reach the domain operation without traversing configuration or lifecycle machinery unrelated to its semantics. The invariant is that framework concepts stay proportional to the complexity they remove.

## Done When
Framework concepts are proportional to the complexity they remove, and the dominant structure of the code is the domain rather than the framework.
