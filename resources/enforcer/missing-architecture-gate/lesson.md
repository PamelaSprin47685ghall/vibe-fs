# missing-architecture-gate — Lesson

## Opinion

### Definition

An architecture gate is missing when a critical dependency or ownership rule exists only as human convention even though the repository can check the rule mechanically. The root-cause is that a known, mechanically decidable graph or ownership invariant has no failing predicate in the check pipeline, so preservation depends on future attention rather than mechanism.

### Governing Principle

An invariant without enforcement is a request to future attention. Architecture is especially vulnerable because violations are locally convenient: importing one internal module often makes a task easier while invisibly degrading the global graph. A static gate moves the burden from memory to mechanism, making the forbidden state expensive at the moment it is introduced rather than during a later redesign.

### Trigger When

Trigger when forbidden imports, layering, ownership boundaries, generated-file rules, or module dependencies are repeatedly reviewed by humans but no build/static check rejects violations.

### Do Not Trigger When

- The principle is inherently semantic and cannot be recognized mechanically without unacceptable false positives.
- The remark is a one-off review comment about a specific import, not a standing architecture rule.
- The build already rejects the forbidden edge via an existing layer, import, or ownership check.
- The remark is a style preference with no architectural consequence if crossed.

### Distinguish From

`missing-invariant-documentation` lacks a durable statement of the rule. `false-gate` has a check that does not actually fail. Tie-break: if the invariant is known and mechanically decidable but left to discipline, this rule; if the wording is missing, `missing-invariant-documentation`; if a check exists but is ineffective, `false-gate`.

### Decision Procedure

Express the boundary as a decidable predicate over repository structure. If a cheap deterministic check can recognize violation, make that check part of the standard gate.

### Examples

- positive: Reviewers keep catching `internal/` imports across packages, but CI has no import-graph check.
- near-miss: The boundary is "do not leak domain meaning into adapters," which no cheap static check can decide.
- counterexample: An existing dependency-cruiser rule already fails forbidden edges in the same command CI runs.

## Advice

### What To Do Now

Encode the architecture rule as a deterministic repository check and add it to the standard verification entry point. The command CI actually runs is who owns the architecture invariant: forbidden edges must be unmergeable, not merely unwelcome in review.

### Why This Matters

A boundary enforced only in review survives exactly until a rushed change, unfamiliar contributor, or innocently convenient import crosses it. The violation is usually cheap locally and expensive globally, so relying on memory places the cost signal at the wrong time.

### Repair Strategy

Define the forbidden graph edge or structural condition precisely, implement the smallest static check, and include a known-bad fixture or self-test proving the gate can turn red.

### Decision Branches

- If a cheap deterministic predicate can recognize the forbidden graph or ownership shape, add it to the standard gate with a known-bad fixture.
- If the rule is semantic and would fail open or flood false positives, record the invariant instead of pretending a gate exists.

### Common Wrong Fixes

- Replace enforcement with more documentation, comments, or reviewer checklists when the property is mechanically decidable.
- Add a gate that scans nothing, matches nothing, or fails open.
- Encode the rule only in a local script that CI never runs.

### Verification

A representative violation must fail the same command CI uses; valid architecture must remain green with low false-positive cost. The invariant is that forbidden edges are unmergeable.

### Done When

The repository itself refuses the forbidden dependency or ownership shape, so preserving the architecture no longer depends on everyone remembering the rule.

### Nudge

Do not ask memory to enforce what the machine can decide. Turn critical architecture boundaries into failing predicates in the build or check pipeline.
