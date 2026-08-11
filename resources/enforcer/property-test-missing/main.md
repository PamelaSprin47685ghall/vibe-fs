# property-test-missing — Main

## What To Do Now
Express the general invariant as a property and test it over generated inputs with useful shrinking and explicit validity constraints.

## Why This Matters
A few examples can establish familiarity but not a universal law. When the code claims behavior over a combinatorial space, property testing lets the machine search for the smallest counterexample to the actual invariant rather than to cases humans happened to foresee.

## Repair Strategy
Write the law first, choose generators that reflect the true domain, avoid filtering so heavily that difficult cases disappear, and preserve found counterexamples as regressions when they reveal meaningful defects.

## Decision Branches
- If a stable “for all valid x…” law exists and tests only sample it, add a generative property with shrinking and keep examples as illustrations.
- If the relevant space is finite and already enumerated, keep exhaustive examples and do not invent a generator.
- If no general law exists, stop; do not add random inputs without a property.

## Common Wrong Fixes
- Generate random inputs without a stable property; randomness alone is not deeper testing.
- Assert only “does not throw” over generated values.
- Filter generators so heavily that the difficult region of the space never appears.
- Replace all examples with properties so the law is no longer readable as a human fixture.

## Verification
Deliberately break the law in a plausible way and confirm generation finds a counterexample with a useful minimized case. The invariant under test must be the stated algebraic law, not “the suite stayed green.”

## Done When
General behavior is guarded by general evidence, while examples remain only as readable illustrations of the same law.
