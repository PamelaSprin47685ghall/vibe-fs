# generic-helper-bucket — Main

## What To Do Now
Empty the generic bucket by assigning each operation to the domain, boundary, or technical concept that actually owns it.

## Why This Matters
A module named `utils` cannot defend a boundary because almost anything can be called useful. Over time it attracts unrelated dependencies and becomes the easiest place to put code that nobody wants to model, concentrating accidental coupling while hiding the reason each function exists.

## Repair Strategy
Classify exports by invariant and reason to change. Move domain operations to domain modules, boundary transformations to adapters, and truly general primitives to narrowly named technical modules whose membership can be stated precisely.

## Decision Branches
- If an export completes a domain concept, move it to that owner.
- If it is a true primitive with one algebra, give it a narrow technical module with an exclusion rule.
- If the module already has many effects and responsibilities under a specific name, that may be `god-module` rather than a nameless bucket.

## Common Wrong Fixes
- Do not split `utils` into `utils2`, `common`, and `helpers`.
- Do not keep the bucket and add a README listing “what not to put here.”
- Do not re-export everything from a new `index` so callers still depend on the grab bag.
- Do not rename `utils` to `core` without an exclusion principle.

## Verification
Every resulting module should answer “what belongs here?” with a sentence strong enough to reject unrelated functions. The invariant is that no module exists merely to hold homeless code.

## Done When
No module exists merely to hold homeless code; every operation’s location communicates the concept, lifecycle, or boundary that owns it.
