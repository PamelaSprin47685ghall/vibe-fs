# illegal-state-representable — Main

## What To Do Now
Model the legitimate states directly and move the invariant to the constructor that first has enough information to prove it. Replace products of unrelated flags/nullable fields with explicit cases or state-specific records so invalid combinations cannot escape.

The owner is the **construction boundary** that knows which combinations are real. Downstream guards are consumers of the invariant, not its owner.

## Why This Matters
Every impossible value admitted by a type becomes recurring work. Readers branch defensively. Serializers invent fallback behavior. Tests cover contradictions nobody wants. Recovery learns to carry garbage farther because “the type allowed it.”

This is not robustness. It is a proof obligation multiplied across the codebase.

A truthful type shrinks the reasoning surface. Once `Paid of ReceiptId` exists, every downstream function can reason from that fact without asking whether the receipt is mysteriously missing. One proof at ingress replaces a forest of repeated checks.

## Repair Strategy
1. List valid states in domain language, independent of the current fields.
2. Identify the data that belongs only to each state.
3. Replace the Cartesian product with a closed sum, state-specific type, or validated constructor.
4. Move transition logic to functions that produce only legal successor states.
5. Keep untrusted DTOs outside the domain; parse them once and fail closed.
6. Delete defensive branches that became unreachable after the stronger construction boundary exists.

## Decision Branches
- If the state space is finite and named, use explicit cases and attach state-specific data to those cases.
- If validity depends on runtime facts, use one atomic constructor returning `Result`; do not leak the rejected representation as a domain value.
- If the loose shape is required for wire compatibility, keep it in the adapter and translate before policy code.
- If every combination is actually meaningful, do nothing. A large state space is not automatically an illegal one.

## Common Wrong Fixes
- Add `validate()` and rely on every caller to remember it.
- Scatter `assert` statements around consumers while leaving construction unchanged.
- Hide the same illegal record behind a helper or facade.
- Add another status flag to explain combinations created by the previous flags.
- Create a “strong” wrapper that still exposes a public constructor for contradictory fields.

## Verification
Attempt to construct each formerly illegal combination through every public construction path, including deserialization/recovery. It must be impossible by type or rejected at the single ingress boundary before a domain value exists.

Then remove a downstream defensive check in a test fixture: the type/constructor should already make the bad case unreachable. The invariant is **representable domain state = legitimate domain state**.

## Done When
Impossible combinations cannot enter domain/application logic, transitions preserve that property, and downstream code no longer spends branches re-proving construction coherence.