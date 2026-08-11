# wrong-rule-composition — Enforcer

## Definition
Rule composition is wrong when dependent checks continue after a prerequisite has failed and emit consequences that no longer have meaning, or when independent checks stop at the first failure and hide other simultaneously valid errors.

## Governing Principle
Composition semantics follow logical dependence. If rule B assumes A established a fact, `A ∧ B` is sequential: failure of A removes the premise that gives B meaning. Independent validations are different: each proposition can be evaluated on the same input, so short-circuiting destroys useful information without improving correctness. The root-cause is applying one evaluation law regardless of logical dependence. One generic “validation pipeline” cannot choose these semantics by syntax alone; the dependency graph determines the algebra.

## Trigger When
Trigger when downstream dependent rules run after prerequisite failure, producing nonsense/cascading errors, or independent validations stop early despite callers needing the complete error set.

## Do Not Trigger When
- Dependent chains deliberately short-circuit and independent rules deliberately accumulate according to their documented semantics.
- A single check where composition choice is vacuous.
- A presentation layer collapses already-classified errors for display without changing evaluation order.
- Fail-fast is used only after a failed premise that makes later rules meaningless, matching the dependency graph.

## Distinguish From
`rule-spaghetti` hides the policy inside imperative flow. `missing-rule-combinator` lacks reusable composition operators. Tie-break: if composition machinery exists but applies the wrong law to the relationship between rules, use this rule; if there are no combinators and flow is ad hoc, use `rule-spaghetti` or `missing-rule-combinator`.

## Decision Procedure
For each pair of rules ask whether one requires a fact established by the other. Dependency implies sequencing and short-circuit; independence permits parallel/accumulating evaluation.

## Examples
- positive: after “email missing,” a later rule reports “email domain not allowed,” or a form returns only the first of three independent field errors.
- near-miss: parse-then-validate short-circuits on parse failure, while independent field rules accumulate.
- counterexample: validation is a long `if` chain with policy mixed into control flow — that is `rule-spaghetti`.

## Nudge
Let logical dependence choose the combinator. Short-circuit when a failed premise makes later rules meaningless; accumulate when independent propositions can all be judged truthfully.
