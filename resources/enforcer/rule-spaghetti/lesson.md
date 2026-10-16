# rule-spaghetti — Lesson

## Opinion

### Definition

Rule spaghetti exists when a business rule is encoded primarily as nested branches, mutable flags, early exits, and temporary state, so its meaning can be recovered only by mentally executing the program. The root-cause is that the policy exists only as nested control flow, so meaning can be recovered only by simulating the program rather than reading named propositions.

### Governing Principle

Policy is a relation between facts and conclusions. Control flow is merely one possible interpreter of that relation. When the interpreter becomes the only specification, readers must simulate paths to discover the rule, and small changes alter both meaning and execution topology at once. A rule is better represented when its propositions are named directly and composition exposes the logic without requiring a private program counter in the reader’s head.

### Trigger When

Trigger when understanding eligibility, permission, validation, routing, or transition policy requires tracing several nested conditionals or mutable intermediates rather than reading named predicates or cases.

### Do Not Trigger When

- Branching mirrors a genuinely sequential dependency and the code already reads as the domain rule with each prerequisite explicit.
- The nesting is a shallow adapter translating one protocol into named domain predicates that then compose cleanly.
- The logic is a mechanical loop over a declared table/cases rather than ad hoc flags.
- Performance-critical code keeps an equivalent named specification/test as the readable policy and the imperative form is a proven translation.

### Distinguish From

missing-rule-combinator lacks reusable composition for already clean rules. wrong-rule-composition combines rules with the wrong failure semantics. This rule is earlier: the policy itself has disappeared into imperative execution. Tie-break: fire here when the policy cannot be read without simulation; fire missing-rule-combinator when named rules exist but lack combinators; fire wrong-rule-composition when combinators exist but apply the wrong law.

### Decision Procedure

State the policy in domain sentences first. Name each proposition, identify which checks depend on prior facts, and choose a composition that preserves those logical dependencies without incidental mutable control state.

### Examples

- positive: eligibility is a nest of `if`/`else` plus three boolean flags mutated along the way; no named predicate states the rule.
- near-miss: a sequential “load user, then check license” chain names each prerequisite and short-circuits on failed premises.
- counterexample: a decision table or pattern match lists named cases that a domain reviewer can map clause-for-clause.

## Advice

### What To Do Now

Rewrite the policy as named predicates, explicit cases, and small composition rules so a reader can recover the business statement without simulating control flow. Named predicates and their composition are who owns the business rule; nested branches and mutable flags are not who owns the policy statement.

### Why This Matters

Nested imperative code makes policy accidental to execution. The same business sentence becomes scattered across branches, assignments, and returns, so changing one clause requires understanding every path that happens to carry it. Declarative structure shortens the proof: the source states which propositions hold and how they combine.

### Repair Strategy

Extract domain queries first, separate dependent checks from independent checks, then compose them with pattern matching, tables, or small combinators. Keep orchestration outside the rule itself.

### Decision Branches

- If the policy is still only in nested branches, extract named propositions and compose them.
- If propositions are already named but glued with mutable flags, replace flags with combinators or cases.
- If composition exists but uses the wrong failure law, that is wrong-rule-composition—fix the combinator, not the names.

### Common Wrong Fixes

- Extract each `if` into a helper while retaining the same opaque sequence.
- Add comments that narrate the maze instead of naming propositions.
- Collapse everything into one boolean expression so dense it is still unreadable.
- Introduce a generic “rules engine” without stating the domain cases.

### Verification

A domain reviewer should be able to map each clause of the rule to a named expression or case, and tests should cover combinations without depending on temporary implementation state. The invariant is: the source states the policy relation, so readers need not reconstruct it from control flow.

### Done When

The code is a readable statement of policy whose control flow follows from the rule, rather than a maze from which the rule must be reconstructed.

### Nudge

Code should let the rule be read, not simulated. Extract named propositions and compose them so the source resembles the policy it enforces.
