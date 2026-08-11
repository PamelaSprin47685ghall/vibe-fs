# random-source-in-logic — Main

## What To Do Now
Inject randomness as an explicit source/seed or generate the choice at the shell and pass the chosen value into pure domain logic.

## Why This Matters
Hidden randomness breaks the equivalence between “same inputs” and “same decision.” That undermines deterministic tests, incident replay, event rebuilding, and explanations of why one branch was chosen over another.

## Repair Strategy
Choose the replay model deliberately: either persist the sampled value as a fact, or persist/control the seed and use a deterministic generator. Keep crypto entropy at security adapters when domain replay does not own it.

## Wrong Fixes
Do not monkey-patch global randomness in tests while leaving production policy dependent on it. Test control is not architectural explicitness.

## Verification
Run the core with the same explicit random input/seed twice; outputs must match. Different random inputs should vary only the decisions the domain intends randomness to influence.

## Done When
Every stochastic domain decision has reproducible entropy provenance rather than an invisible call to ambient randomness.
