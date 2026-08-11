# missing-rule-combinator — Enforcer

## Definition
A rule combinator is missing when several policies share the same input/output algebra but are repeatedly sequenced by bespoke control flow rather than composed through that algebra.

## Governing Principle
Repeated rule composition reveals a language. If each rule is `A → Result<B,E>` or `A → E option`, then sequencing, short-circuiting, and accumulation are not incidental syntax; they are operations over that common shape. Naming those operations once turns imperative repetition into a small algebra whose laws can be tested independently.

## Trigger When
Trigger when three or more rules with the same semantic shape are manually chained, folded, accumulated, or short-circuited in several places.

## Do Not Trigger When
Do not trigger when only one or two isolated rules exist or their apparently similar signatures carry different semantics that would make one combinator misleading.

## Distinguish From
wrong-rule-composition chooses the wrong short-circuit/accumulation semantics. rule-spaghetti hides policy in control flow. This rule concerns absence of a reusable composition vocabulary after a real algebra has emerged.

## Decision Procedure
Write the rule signature and the desired composition laws. If several callers reimplement those laws, define the smallest named combinators and use them everywhere.

## Nudge
When rules share a shape, composition itself becomes domain knowledge. Name that algebra once instead of rewriting its control flow at every call site.
