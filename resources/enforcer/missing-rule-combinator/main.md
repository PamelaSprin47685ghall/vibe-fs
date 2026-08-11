# missing-rule-combinator — Main

## What To Do Now
Extract the shared rule signature and define the smallest combinators for its real semantics: sequential short-circuit, independent error accumulation, mapping, or conjunction.

## Why This Matters
Handwritten rule chains duplicate not just syntax but policy about how failures compose. Once that policy has one algebraic owner, readers can reason at the level of rules rather than temporary variables and nested conditionals.

## Repair Strategy
Keep each rule small and named, make the combinator generic only over the stable input/output shape, and write law-like tests for ordering and error behavior.

## Wrong Fixes
Do not create a large rules engine, DSL, or framework merely to avoid a few function calls. The useful abstraction is the minimal composition law already present in the code.

## Verification
Equivalent rule sets should produce the same outcomes regardless of call site, and dependent versus independent rules should compose according to their explicitly chosen law.

## Done When
Business policy reads as a composition of named rules, while the mechanics of sequencing and error collection exist in one small reusable vocabulary.
