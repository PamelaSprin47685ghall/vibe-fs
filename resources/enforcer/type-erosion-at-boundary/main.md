# type-erosion-at-boundary — Main

## What To Do Now
Contain dynamic decoding and unchecked operations inside the adapter, then expose a validated domain type to every inward caller. Repair at ingress: who owns the adapter owns the typed construction.

## Why This Matters
Weak representations move uncertainty rather than remove it. If a cast or dynamic field access survives past ingress, downstream code repeatedly asks questions the boundary could have settled once, and failures occur after provenance has been lost.

## Repair Strategy
Parse, validate, normalize, and construct the strong type at the edge. Keep raw payloads private to the adapter and eliminate casts in policy code by making the boundary return the exact domain alternatives it can establish.

## Decision Branches
If dynamic or unchecked forms are still visible to domain/application code, stop them at the adapter and return a validated type.
If decoding already yields a constructor-enforced domain value, do not reintroduce `any` or casts inward.

## Common Wrong Fixes
- Wrap `any` in a generically named object and call it typed.
- Add assertions at every call site instead of one validated constructor at the edge.
- Use unchecked casts “just for this handler” that then leak into shared policy.

## Verification
Invariant: type uncertainty has one owner at ingress and cannot leak inward. Search inward layers for dynamic access and unchecked casts. Malformed input should fail at the boundary; valid input should emerge as a type downstream can trust without rechecking shape.

## Done When
Type uncertainty has one owner at ingress and cannot leak inward as a recurring proof obligation.
