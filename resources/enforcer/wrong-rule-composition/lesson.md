# wrong-rule-composition — Lesson

## Opinion

### Definition

Rule composition is wrong when dependent checks continue after a prerequisite has failed and emit consequences that no longer have meaning, or when independent checks stop at the first failure and hide other simultaneously valid errors.

### Governing Principle

Composition semantics follow logical dependence. If rule B assumes A established a fact, `A ∧ B` is sequential: failure of A removes the premise that gives B meaning. Independent validations are different: each proposition can be evaluated on the same input, so short-circuiting destroys useful information without improving correctness. The root-cause is applying one evaluation law regardless of logical dependence. One generic “validation pipeline” cannot choose these semantics by syntax alone; the dependency graph determines the algebra.

### Trigger When

Trigger when downstream dependent rules run after prerequisite failure, producing nonsense/cascading errors, or independent validations stop early despite callers needing the complete error set.

### Do Not Trigger When

- Dependent chains deliberately short-circuit and independent rules deliberately accumulate according to their documented semantics.
- A single check where composition choice is vacuous.
- A presentation layer collapses already-classified errors for display without changing evaluation order.
- Fail-fast is used only after a failed premise that makes later rules meaningless, matching the dependency graph.

### Distinguish From

`rule-spaghetti` hides the policy inside imperative flow. `missing-rule-combinator` lacks reusable composition operators. Tie-break: if composition machinery exists but applies the wrong law to the relationship between rules, use this rule; if there are no combinators and flow is ad hoc, use `rule-spaghetti` or `missing-rule-combinator`.

### Decision Procedure

For each pair of rules ask whether one requires a fact established by the other. Dependency implies sequencing and short-circuit; independence permits parallel/accumulating evaluation.

### Examples

- positive: after “email missing,” a later rule reports “email domain not allowed,” or a form returns only the first of three independent field errors.
- near-miss: parse-then-validate short-circuits on parse failure, while independent field rules accumulate.
- counterexample: validation is a long `if` chain with policy mixed into control flow — that is `rule-spaghetti`.

## Advice

### What To Do Now

Classify rules by dependence, then use sequential short-circuit composition for prerequisite chains and accumulating composition for independent validations. Choose the combinator at the premise: who owns each rule’s required fact owns whether it short-circuits or accumulates.

### Why This Matters

Error behavior is part of policy. Running a rule after its premise failed manufactures misleading errors; stopping after one independent failure withholds other facts the same input already proves. The right composition preserves the logical structure of the domain rather than imposing one control-flow habit everywhere.

### Repair Strategy

Draw dependencies between rules, separate prerequisite establishment from independent constraints, and encode both semantics in small named combinators. Keep error types rich enough to distinguish prerequisite failure from independently accumulated violations.

### Decision Branches

If rule B requires a fact established by A, sequence them and short-circuit when A fails.
If rules are independent on the same input, accumulate their results instead of stopping at the first failure.

### Common Wrong Fixes

- Choose “fail fast” or “collect all” as a project-wide ideology.
- Keep cascading nonsense errors and filter them in the UI.
- Duplicate each rule’s prerequisite checks instead of expressing dependence in the combinator.

### Verification

Invariant: reported errors are exactly the meaningful ones given logical dependence. Cases with a failed prerequisite plus downstream rules must report only reachable errors; cases with several independent violations must return the complete independent set.

### Done When

The rule engine’s control semantics are derivable from logical dependence, so every reported error is meaningful and no independent truth is hidden by arbitrary evaluation order.

### Nudge

Let logical dependence choose the combinator. Short-circuit when a failed premise makes later rules meaningless; accumulate when independent propositions can all be judged truthfully.
