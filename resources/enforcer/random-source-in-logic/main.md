# random-source-in-logic — Main

## What To Do Now
Inject randomness as an explicit source/seed or generate the choice at the shell and pass the chosen value into pure domain logic.

## Why This Matters
Hidden randomness breaks the equivalence between “same inputs” and “same decision.” That undermines deterministic tests, incident replay, event rebuilding, and explanations of why one branch was chosen over another.

## Repair Strategy
Choose the replay model deliberately: either persist the sampled value as a fact, or persist/control the seed and use a deterministic generator. Keep crypto entropy at security adapters when domain replay does not own it.

## Decision Branches
- If the domain must replay the decision, pass a seed or sampled value into the core and persist that value with the event.
- If the entropy is cryptographic and not a business choice, keep it in the security adapter and out of domain policy.
- If tests currently monkey-patch a global RNG, replace that with the same explicit port production will use.

## Common Wrong Fixes
- Monkey-patch global randomness in tests while leaving production policy dependent on it.
- Seed `Math.random` once at process start and treat that as an explicit input.
- Generate a UUID inside the core “for convenience” and only log it after the fact.
- Disable randomness in tests so the hidden call is never exercised.

## Verification
Run the core with the same explicit random input/seed twice; outputs must match. Different random inputs should vary only the decisions the domain intends randomness to influence. The invariant is: identical declared inputs, including entropy provenance, yield identical domain decisions.

## Done When
Every stochastic domain decision has reproducible entropy provenance rather than an invisible call to ambient randomness.
