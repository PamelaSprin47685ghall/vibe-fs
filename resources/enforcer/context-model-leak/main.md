# context-model-leak — Main

## What To Do Now
Split the shared model into context-owned types and define explicit translations for the small set of facts that legitimately cross boundaries.

## Why This Matters
A universal model creates semantic coupling disguised as reuse. Changes become contagious because every context can see fields introduced for every other context. Optional fields accumulate to represent “not meaningful here,” and authorization or lifecycle rules blur because the type no longer tells which interpretation is active.

## Repair Strategy
Start from each context’s questions and invariants, not from the existing field list. Create the smallest model that makes those rules natural. Translate identifiers and stable facts at the boundary instead of importing a foreign context wholesale.

## Wrong Fixes
Do not add more nullable fields, view flags, or context enums to the universal model. That turns bounded contexts into modes of one giant object rather than restoring separate meanings.

## Verification
A context should compile and make sense using only its own model plus explicit boundary contracts. Fields irrelevant to that context should be unrepresentable there.

## Done When
Each model has one semantic owner and one reason to change, while cross-context communication carries facts rather than leaking another context’s representation.
