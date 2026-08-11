# property-test-missing — Main

## What To Do Now
Identify the invariant (round-trip, associativity, idempotence, normalization fixpoint, etc.). Add property-based tests that generate inputs across the space and assert the law. Keep a few example tests for readability, but do not treat them as sufficient coverage of a general rule.

## Repair Strategy
Name the law in a comment or test title. Prefer a small generator over dozens of brittle fixtures. If the domain type is hard to generate, introduce a smart constructor or shrink strategy rather than abandoning properties.

## Decision Branches
If no clear invariant exists, document why example tests suffice and stop. If an invariant is suspected but unproven, write the property first and let failures refine the law. If only boundary cases matter, keep focused examples and skip generative noise.

## Wrong Fixes
Spamming random inputs without an asserted law. Duplicating the implementation inside the property. Replacing all examples with opaque generators that hide regressions.

## Verification
Run the new properties repeatedly; confirm they fail on a deliberate law break and pass on the fixed implementation.

## Done When
At least one property test encodes each claimed general invariant, and example-only coverage is no longer the sole proof.

## Scope and Authority
Applies to pure domain operations with stated or obvious laws. Does not mandate property tests for every UI click path.
