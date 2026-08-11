# context-model-leak — Main

## What To Do Now
Split the shared model into context-owned types and define explicit translations for the small set of facts that legitimately cross boundaries.

## Why This Matters
A universal model creates semantic coupling disguised as reuse. Changes become contagious because every context can see fields introduced for every other context. Optional fields accumulate to represent “not meaningful here,” and authorization or lifecycle rules blur because the type no longer tells which interpretation is active.

## Repair Strategy
Start from each context’s questions and invariants, not from the existing field list. Create the smallest model that makes those rules natural. Translate identifiers and stable facts at the boundary instead of importing a foreign context wholesale.

## Decision Branches
- If one type answers different contexts’ questions, split it and translate only shared facts.
- If a field is meaningless in a context, it must be unrepresentable there, not nullable on a master object.
- If a tiny value object truly has identical meaning everywhere, sharing it is not a leak.

## Common Wrong Fixes
- Do not add more nullable fields to the universal model.
- Do not add view flags or context enums to keep one mega-type.
- Do not copy the master model into each package without changing ownership of meaning.
- Do not “namespace” fields on the same object (`authEmail`, `billingEmail`) as a substitute for split types.

## Verification
A context should compile and make sense using only its own model plus explicit boundary contracts. Fields irrelevant to that context should be unrepresentable there. The invariant is that each model has one semantic owner and one reason to change.

## Done When
Each model has one semantic owner and one reason to change, while cross-context communication carries facts rather than leaking another context’s representation.
