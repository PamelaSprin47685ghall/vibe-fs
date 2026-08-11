# pattern-sprawl — Enforcer

## Definition
Pattern sprawl occurs when factories, visitors, strategy classes, interface hierarchies, builders, or other pattern machinery simulate distinctions the language can express more directly with closed data and first-class functions.

## Governing Principle
A design pattern is historically a compensation mechanism: it encodes a missing language capability as a convention of objects and indirection. When the language already has sum types, exhaustive matching, closures, modules, and immutable values, keeping the simulation preserves ceremony after the original limitation has disappeared. Abstraction should collapse when the host language can state the same law directly.

## Trigger When
Trigger when class/interface scaffolding primarily exists to select among finite cases, inject behavior, traverse closed data, construct values, or compose functions that native language features can express with less indirection.

## Do Not Trigger When
Do not trigger when runtime extensibility across independently deployed components or open-world substitution is a real requirement that closed algebraic data cannot satisfy.

## Distinguish From
incidental-complexity-dominates is broader. premature-unification concerns false abstraction. This rule specifically targets pattern machinery whose semantic job has become native syntax.

## Decision Procedure
State what variability the pattern represents. If the set is closed, use data + exhaustive match; if behavior is the variable, pass a function; if both are simple, prefer composition over hierarchy.

## Nudge
Do not preserve a simulation after the language gains the thing being simulated. Express closed data as data and variable behavior as functions; keep class machinery only for genuinely open-world contracts.
