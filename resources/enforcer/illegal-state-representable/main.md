# illegal-state-representable — Main

## What To Do Now
Replace the field/flag product with explicit domain states so only legitimate combinations can be constructed.

## Why This Matters
Every illegal value admitted by a type becomes a proof obligation for all code that reads it. Guards multiply because the model keeps re-asking whether the value is real. A stronger type pays that proof once, at construction, and lets the rest of the program reason under a smaller truthful state space.

## Repair Strategy
List valid states and the data meaningful in each. Model them as distinct cases or validated constructors, then make state transitions explicit and exhaustive. Remove fields whose only purpose was to be nullable outside their phase.

## Wrong Fixes
Do not add more runtime assertions around the same impossible combinations. Assertions detect a modeling failure after construction; they do not prevent the false world from entering the program.

## Verification
Attempt to construct each formerly illegal combination. It should be impossible by type/constructor rather than merely rejected later.

## Done When
Representable state equals legitimate domain state, and downstream code no longer spends branches proving that its input was constructed coherently.
