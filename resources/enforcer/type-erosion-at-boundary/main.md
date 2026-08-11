# type-erosion-at-boundary — Main

## What To Do Now
Contain dynamic decoding and unchecked operations inside the adapter, then expose a validated domain type to every inward caller.

## Why This Matters
Weak representations move uncertainty rather than remove it. If a cast or dynamic field access survives past ingress, downstream code repeatedly asks questions the boundary could have settled once, and failures occur after provenance has been lost.

## Repair Strategy
Parse, validate, normalize, and construct the strong type at the edge. Keep raw payloads private to the adapter and eliminate casts in policy code by making the boundary return the exact domain alternatives it can establish.

## Wrong Fixes
Do not wrap `any` in a generically named object and call it typed. A nominal shell without validated semantics merely hides erosion.

## Verification
Search inward layers for dynamic access and unchecked casts. Malformed external input should fail at the boundary; valid input should emerge as a type downstream code can trust without rechecking shape.

## Done When
Type uncertainty has one owner at ingress and cannot leak inward as a recurring proof obligation.
