# missing-rule-combinator — Enforcer

## Definition
A rule combinator is missing when several policies share the same input/output algebra but are repeatedly sequenced by bespoke control flow rather than composed through that algebra. The root-cause is that composition laws—short-circuit, accumulation, mapping—remain handwritten control flow even after a shared rule algebra has already emerged.

## Governing Principle
Repeated rule composition reveals a language. If each rule is `A → Result<B,E>` or `A → E option`, then sequencing, short-circuiting, and accumulation are not incidental syntax; they are operations over that common shape. Naming those operations once turns imperative repetition into a small algebra whose laws can be tested independently.

## Trigger When
Trigger when three or more rules with the same semantic shape are manually chained, folded, accumulated, or short-circuited in several places.

## Do Not Trigger When
- Only one or two isolated rules exist, so a combinator would be speculative.
- Apparently similar signatures carry different semantics that would make one combinator misleading.
- A named combinator already owns sequencing and callers use it.
- The repetition is copy-pasted domain policy itself, not the sequencing/accumulation mechanics around a shared signature.

## Distinguish From
`wrong-rule-composition` chooses the wrong short circuit or accumulation semantics. `rule-spaghetti` hides policy in control flow. Tie-break: if a real algebra has emerged but composition vocabulary is absent, this rule; if combinators exist but the wrong law is used, `wrong-rule-composition`; if policy is tangled in ad hoc control flow without a shared shape, `rule-spaghetti`.

## Decision Procedure
Write the rule signature and the desired composition laws. If several callers reimplement those laws, define the smallest named combinators and use them everywhere.

## Examples
- positive: Five validators of type `Input → Result<Input, Error>` are nested with copy-pasted `if err return` in three call sites.
- near-miss: Two one-off checks with different failure meanings; forcing one combinator would hide that difference.
- counterexample: Rules already compose through `andThen` / `all` with tests for the composition laws.

## Nudge
When rules share a shape, composition itself becomes domain knowledge. Name that algebra once instead of rewriting its control flow at every call site.
