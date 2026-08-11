# phase-flag-accumulation — Main

## What To Do Now
Replace interacting lifecycle flags with a smaller explicit state model or structured control flow whose valid phases and transitions are named.

## Why This Matters
Flags scale as combinations, while real lifecycles usually scale as a sequence or small graph. The gap becomes illegal states and conditional logic whose purpose is to rule out worlds the representation should never have admitted.

## Repair Strategy
List valid lifecycle states and the data meaningful in each. Encode them as closed cases or keep phase-local data inside the control scope that owns it. Remove obsolete flags rather than mirroring the new state with old booleans.

## Wrong Fixes
Do not add a master `phase` enum while retaining all old flags “for convenience.” Two lifecycle representations immediately recreate duplicated truth.

## Verification
Every valid transition should be explicit and every former contradictory flag combination should become unconstructable.

## Done When
The lifecycle has one representation whose state space matches reality, and new behavior extends named transitions rather than adding another boolean patch.
