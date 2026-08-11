# premature-unification — Main

## What To Do Now
Separate concepts that were unified by appearance rather than shared invariant. Let each owner evolve independently until common knowledge—not merely common shape—becomes undeniable.

## Why This Matters
A premature abstraction converts coincidental similarity into a coupling contract. The first real divergence then appears as "exceptions" to the abstraction, and the code begins accumulating flags and optional hooks to preserve a unity the domain never had.

## Repair Strategy
Split the model/API along lifecycle and reason to change. Duplicate small amounts of code if necessary; observe whether future changes remain parallel. Extract only the part that repeatedly changes for the same reason across both contexts.

## Decision Branches
- If a change to one concept need not change the other, split the types/APIs even if they look alike today.
- If a shared invariant has already emerged, extract only that knowledge—not the surrounding coincidental shape.

## Common Wrong Fixes
- Keep one generic type with context flags, preserving false unity.
- Extract a mega-helper that takes a mode enum to serve both owners.
- Delay the split because "they might become the same later."

## Verification
A change specific to one concept should no longer require conditional logic or edits in the other. The invariant is that shared abstractions correspond to shared knowledge.

## Done When
Shared abstractions correspond to shared knowledge, and independent concepts are allowed to look alike without being forced to live together.
