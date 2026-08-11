# pattern-sprawl — Enforcer

## Definition
Pattern sprawl occurs when factories, visitors, strategy classes, interface hierarchies, builders, or other pattern machinery simulate distinctions the language can express more directly with closed data and first-class functions. The root-cause is that pattern machinery continues to simulate a language capability that is now native, so ceremony remains after the original limitation has disappeared.

## Governing Principle
A design pattern is historically a compensation mechanism: it encodes a missing language capability as a convention of objects and indirection. When the language already has sum types, exhaustive matching, closures, modules, and immutable values, keeping the simulation preserves ceremony after the original limitation has disappeared. Abstraction should collapse when the host language can state the same law directly.

## Trigger When
Trigger when class/interface scaffolding primarily exists to select among finite cases, inject behavior, traverse closed data, construct values, or compose functions that native language features can express with less indirection.

## Do Not Trigger When
- Runtime extensibility across independently deployed components or open-world substitution is a real requirement that closed algebraic data cannot satisfy.
- The pattern encodes a lifecycle or ownership boundary the language does not provide.
- The "pattern" is already the native form (a function, a union, a module).

## Distinguish From
incidental-complexity-dominates is broader design weight. premature-unification concerns false abstraction. Tie-break: if pattern machinery's semantic job is now native syntax, this rule; if complexity is high for many reasons, incidental-complexity-dominates; if one abstraction falsely merges different knowledge, premature-unification.

## Decision Procedure
State what variability the pattern represents. If the set is closed, use data + exhaustive match; if behavior is the variable, pass a function; if both are simple, prefer composition over hierarchy.

## Examples
- positive: A five-class visitor hierarchy exists only to switch on a closed AST the language can match exhaustively.
- near-miss: A plugin interface is loaded from independently deployed packages; closed data cannot name those types.
- counterexample: Closed variants are a union with exhaustive match and behavior is passed as functions.

## Nudge
Do not preserve a simulation after the language gains the thing being simulated. Express closed data as data and variable behavior as functions; keep class machinery only for genuinely open-world contracts.
