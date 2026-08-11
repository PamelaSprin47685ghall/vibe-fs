# wrong-rule-composition — Enforcer

## Definition
Rule composition is wrong when dependent checks continue after a prerequisite has failed and emit consequences that no longer have meaning, or when independent checks stop at the first failure and hide other simultaneously valid errors.

## Governing Principle
Composition semantics follow logical dependence. If rule B assumes A established a fact, `A ∧ B` is sequential: failure of A removes the premise that gives B meaning. Independent validations are different: each proposition can be evaluated on the same input, so short-circuiting destroys useful information without improving correctness. One generic “validation pipeline” cannot choose these semantics by syntax alone; the dependency graph determines the algebra.

## Trigger When
Trigger when downstream dependent rules run after prerequisite failure, producing nonsense/cascading errors, or independent validations stop early despite callers needing the complete error set.

## Do Not Trigger When
Do not trigger when dependent chains deliberately short-circuit and independent rules deliberately accumulate according to their documented semantics.

## Distinguish From
rule-spaghetti hides the policy inside imperative flow. missing-rule-combinator lacks reusable composition operators. This rule has composition machinery but applies the wrong law to the relationship between rules.

## Decision Procedure
For each pair of rules ask whether one requires a fact established by the other. Dependency implies sequencing and short-circuit; independence permits parallel/accumulating evaluation.

## Nudge
Let logical dependence choose the combinator. Short-circuit when a failed premise makes later rules meaningless; accumulate when independent propositions can all be judged truthfully.
