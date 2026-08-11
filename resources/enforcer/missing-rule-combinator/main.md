# missing-rule-combinator — Main

## What To Do Now
Extract the shared rule signature and define the smallest combinators for its real semantics: sequential short-circuit, independent error accumulation, mapping, or conjunction. That shared signature is who owns composition: one vocabulary for how failures combine, not a nested `if err return` at every call site.

## Why This Matters
Handwritten rule chains duplicate not just syntax but policy about how failures compose. Once that policy has one algebraic owner, readers can reason at the level of rules rather than temporary variables and nested conditionals.

## Repair Strategy
Keep each rule small and named, make the combinator generic only over the stable input/output shape, and write law-like tests for ordering and error behavior.

## Decision Branches
- If several callers reimplement the same short-circuit or accumulation law, name that law once and replace the copies.
- If signatures look alike but composition meanings differ, keep separate combinators or do not unify.

## Common Wrong Fixes
- Create a large rules engine, DSL, or framework merely to avoid a few function calls.
- Invent a combinator whose law does not match how failures actually compose.
- Genericize over unstable shapes so every new rule needs escape hatches.

## Verification
Equivalent rule sets should produce the same outcomes regardless of call site, and dependent versus independent rules should compose according to their explicitly chosen law. The invariant is that composition semantics live in one vocabulary.

## Done When
Business policy reads as a composition of named rules, while the mechanics of sequencing and error collection exist in one small reusable vocabulary.
