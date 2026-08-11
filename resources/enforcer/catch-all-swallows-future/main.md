# catch-all-swallows-future — Main

## What To Do Now
Replace the generic fallback with explicit exhaustive cases wherever the domain is closed and new variants deserve deliberate semantics.

## Why This Matters
A wildcard trades a present convenience for a future blind spot. Without it, extending a finite type produces a map of every place whose assumptions must be reconsidered. With it, the compiler stays silent and old behavior is silently inherited by a case nobody had in mind when the branch was written.

## Repair Strategy
Model the domain as a closed set where appropriate, enumerate cases, and reserve explicit `Unknown` or extension handling for protocols that truly permit unknown values.

## Decision Branches
- If the domain is closed and new cases need human judgment, remove the catch-all and force exhaustiveness.
- If the protocol is intentionally open, make the unknown path an explicit named contract, not an accidental `default`.
- If a default still exists, it must not apply domain meaning to future variants it has never seen.

## Common Wrong Fixes
- Do not replace `_` with a default function that still swallows every future case.
- Do not log-and-ignore a new variant merely to preserve compilation.
- Do not map unknown to the “most similar” existing case.
- Do not keep a catch-all “just for safety” on a closed enum.

## Verification
Introduce or mentally substitute a new case. The relevant decision points should fail to compile or fail a focused test until the new semantics are chosen. The invariant is that future domain growth produces a visible obligation instead of silently inheriting a fallback.

## Done When
Future domain growth produces visible obligations instead of silently inheriting a fallback written for a different world.
