# legacy-cruft-retained — Main

## What To Do Now
Obsolete compatibility code is being retained. Complete the clean break and remove the old surface.

## Repair Strategy
1. Confirm the ScoreWhen condition against the current change, not a guessed future risk.
2. Apply the nudge at the owning boundary; do not paper over symptoms downstream.
3. Remove obsolete paths, adapters, or temporary flags created by the wrong fix.
4. Leave a mechanical check or named type where the boundary can regress.

## Decision Branches
- If the smell is real and local: apply the nudge and verify the boundary.
- If a sibling tip fits better: switch to that tip rather than stretching this one.
- If the boundary is already explicit and guarded: stop; this tip does not apply.

## Wrong Fixes
- Renaming without changing ownership or representation.
- Adding comments or TODOs instead of a type, test, or gate.
- Dual-writing old and new paths "just in case".
- Broad refactors that leave half-finished ownership.

## Verification
- Re-read the changed boundary and confirm the ScoreWhen condition no longer holds.
- Run the narrowest check that would fail if the old smell returned.
- Ensure no leftover scaffolding or compatibility shim remains without an owner.

## Done When
- The nudge is applied at the source boundary.
- Obsolete dual paths are gone.
- A reader can see the concept, ownership, and guard without tribal knowledge.

## Scope and Authority
- Tip substance comes from ScoreWhen/Nudge; do not invent extra product requirements.
- Prefer the smallest change that closes the boundary; escalate only when ownership is unclear.
- Why (context): Obsolete code, aliases, compatibility branches, or old names are kept despite an explicit clean-break policy.
