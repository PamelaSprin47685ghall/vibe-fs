# generic-helper-bucket — Main

## What To Do Now
Empty the generic bucket by assigning each operation to the domain, boundary, or technical concept that actually owns it.

## Why This Matters
A module named `utils` cannot defend a boundary because almost anything can be called useful. Over time it attracts unrelated dependencies and becomes the easiest place to put code that nobody wants to model, concentrating accidental coupling while hiding the reason each function exists.

## Repair Strategy
Classify exports by invariant and reason to change. Move domain operations to domain modules, boundary transformations to adapters, and truly general primitives to narrowly named technical modules whose membership can be stated precisely.

## Wrong Fixes
Do not split `utils` into `utils2`, `common`, and `helpers`. Smaller buckets without ownership reproduce the same entropy.

## Verification
Every resulting module should answer “what belongs here?” with a sentence strong enough to reject unrelated functions.

## Done When
No module exists merely to hold homeless code; every operation’s location communicates the concept, lifecycle, or boundary that owns it.
