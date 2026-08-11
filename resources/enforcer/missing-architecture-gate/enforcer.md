# missing-architecture-gate — Enforcer

## Definition
An architecture gate is missing when a critical dependency or ownership rule exists only as human convention even though the repository can check the rule mechanically.

## Governing Principle
An invariant without enforcement is a request to future attention. Architecture is especially vulnerable because violations are locally convenient: importing one internal module often makes a task easier while invisibly degrading the global graph. A static gate moves the burden from memory to mechanism, making the forbidden state expensive at the moment it is introduced rather than during a later redesign.

## Trigger When
Trigger when forbidden imports, layering, ownership boundaries, generated-file rules, or module dependencies are repeatedly reviewed by humans but no build/static check rejects violations.

## Do Not Trigger When
- The principle is inherently semantic and cannot be recognized mechanically without unacceptable false positives.
- The remark is a one-off review comment about a specific import, not a standing architecture rule.
- The build already rejects the forbidden edge via an existing layer, import, or ownership check.

## Distinguish From
missing-invariant-documentation lacks a durable statement of the rule. false-gate has a check that does not actually fail. Tie-break: if the invariant is known and mechanically decidable but left to discipline, this rule; if the wording is missing, missing-invariant-documentation; if a check exists but is ineffective, false-gate.

## Decision Procedure
Express the boundary as a decidable predicate over repository structure. If a cheap deterministic check can recognize violation, make that check part of the standard gate.

## Examples
- positive: Reviewers keep catching `internal/` imports across packages, but CI has no import-graph check.
- near-miss: The boundary is "do not leak domain meaning into adapters," which no cheap static check can decide.
- counterexample: An existing dependency-cruiser rule already fails forbidden edges in the same command CI runs.

## Nudge
Do not ask memory to enforce what the machine can decide. Turn critical architecture boundaries into failing predicates in the build or check pipeline.
