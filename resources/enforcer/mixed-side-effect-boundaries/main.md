# mixed-side-effect-boundaries — Main

## What To Do Now
Unrelated side-effect boundaries are mixed together. Isolate each effect behind a narrow port and keep policy pure.

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
- Why (context): A single function or module simultaneously owns unrelated effects such as storage, network, process control, UI, Git, and policy decisions.
