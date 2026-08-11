# missing-architecture-gate — Enforcer

## Definition
An architecture gate is missing when a critical dependency or ownership rule exists only as human convention even though the repository can check the rule mechanically.

## Governing Principle
An invariant without enforcement is a request to future attention. Architecture is especially vulnerable because violations are locally convenient: importing one internal module often makes a task easier while invisibly degrading the global graph. A static gate moves the burden from memory to mechanism, making the forbidden state expensive at the moment it is introduced rather than during a later redesign.

## Trigger When
Trigger when forbidden imports, layering, ownership boundaries, generated-file rules, or module dependencies are repeatedly reviewed by humans but no build/static check rejects violations.

## Do Not Trigger When
Do not trigger for principles that are inherently semantic and cannot be recognized mechanically without unacceptable false positives.

## Distinguish From
missing-invariant-documentation lacks a durable statement of the rule. false-gate has an ineffective check. This rule assumes the invariant is known but leaves enforcement to discipline.

## Decision Procedure
Express the boundary as a decidable predicate over repository structure. If a cheap deterministic check can recognize violation, make that check part of the standard gate.

## Nudge
Do not ask memory to enforce what the machine can decide. Turn critical architecture boundaries into failing predicates in the build or check pipeline.
