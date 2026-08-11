# clone-and-mutate-derived — Main

## What To Do Now
Replace clone-then-patch construction with an explicit immutable constructor or record copy whose preserved fields are intentionally part of the same value semantics.

## Why This Matters
Clone-and-mutate makes future fields opt in to propagation automatically. A field added next month can flow into derived values nobody reviewed, because omission means inheritance. That is the opposite of a stable constructor, where new information must be deliberately supplied or deliberately defaulted.

## Repair Strategy
Name the semantic relationship between source and derived value. Pass only the source facts that relationship preserves, and require all other fields explicitly. Keep mutation local only as an implementation detail that cannot escape construction.

## Wrong Fixes
Do not deep-clone more carefully. Do not add comments listing fields that “should stay the same.” The defect is that structure, rather than domain meaning, decides what is inherited.

## Verification
Add or imagine a new field on the source type. The derivation should force an explicit decision about that field rather than copying it silently.

## Done When
The derived value’s contents can be explained from its constructor and domain relation alone, without knowing which fields happened to exist on a mutable prototype.
