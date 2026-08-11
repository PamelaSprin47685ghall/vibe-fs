# rule-spaghetti — Enforcer

## Definition
Rule spaghetti exists when a business rule is encoded primarily as nested branches, mutable flags, early exits, and temporary state, so its meaning can be recovered only by mentally executing the program.

## Governing Principle
Policy is a relation between facts and conclusions. Control flow is merely one possible interpreter of that relation. When the interpreter becomes the only specification, readers must simulate paths to discover the rule, and small changes alter both meaning and execution topology at once. A rule is better represented when its propositions are named directly and composition exposes the logic without requiring a private program counter in the reader’s head.

## Trigger When
Trigger when understanding eligibility, permission, validation, routing, or transition policy requires tracing several nested conditionals or mutable intermediates rather than reading named predicates or cases.

## Do Not Trigger When
Do not trigger when branching mirrors a genuinely sequential dependency and the code already reads as the domain rule with each prerequisite explicit.

## Distinguish From
missing-rule-combinator lacks reusable composition for already clean rules. wrong-rule-composition combines rules with the wrong failure semantics. This rule is earlier: the policy itself has disappeared into imperative execution.

## Decision Procedure
State the policy in domain sentences first. Name each proposition, identify which checks depend on prior facts, and choose a composition that preserves those logical dependencies without incidental mutable control state.

## Nudge
Code should let the rule be read, not simulated. Extract named propositions and compose them so the source resembles the policy it enforces.
