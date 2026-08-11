# illegal-state-representable — Main

## What To Do Now
Replace the field/flag product with explicit domain states so only legitimate combinations can be constructed. The constructing type—sum of valid cases or closed constructor—is who owns the state-space invariant that representable values equal legitimate domain states; downstream guards are not.

## Why This Matters
Every illegal value admitted by a type becomes a proof obligation for all code that reads it. Guards multiply because the model keeps re-asking whether the value is real. A stronger type pays that proof once, at construction, and lets the rest of the program reason under a smaller truthful state space.

## Repair Strategy
List valid states and the data meaningful in each. Model them as distinct cases or validated constructors, then make state transitions explicit and exhaustive. Remove fields whose only purpose was to be nullable outside their phase.

## Decision Branches
- If the extra combinations are never meaningful, replace the product with a sum of valid cases.
- If some combinations are only illegal after validation, keep a transport shape and close construction at the domain boundary.

## Common Wrong Fixes
- Do not add more runtime assertions around the same impossible combinations. Assertions detect a modeling failure after construction; they do not prevent the false world from entering the program.
- Do not document “this field is required when flag is true” and leave the type open.
- Do not hide the product behind a helper that still returns the illegal record type.

## Verification
Attempt to construct each formerly illegal combination. It should be impossible by type/constructor rather than merely rejected later. That construction barrier is the invariant: representable state equals legitimate domain state.

## Done When
Representable state equals legitimate domain state, and downstream code no longer spends branches proving that its input was constructed coherently.
