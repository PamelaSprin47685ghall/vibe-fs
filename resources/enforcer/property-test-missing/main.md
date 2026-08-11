# property-test-missing — Main

## What To Do Now
Express the general invariant as a property and test it over generated inputs with useful shrinking and explicit validity constraints.

## Why This Matters
A few examples can establish familiarity but not a universal law. When the code claims behavior over a combinatorial space, property testing lets the machine search for the smallest counterexample to the actual invariant rather than to cases humans happened to foresee.

## Repair Strategy
Write the law first, choose generators that reflect the true domain, avoid filtering so heavily that difficult cases disappear, and preserve found counterexamples as regressions when they reveal meaningful defects.

## Wrong Fixes
Do not generate random inputs without a stable property; randomness alone is not deeper testing. Do not assert only “does not throw.”

## Verification
Deliberately break the law in a plausible way and confirm generation finds a counterexample with a useful minimized case.

## Done When
General behavior is guarded by general evidence, while examples remain only as readable illustrations of the same law.
