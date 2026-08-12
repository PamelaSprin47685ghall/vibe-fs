# generic-helper-bucket — Main

## What To Do Now
Empty the junk drawer by assigning each operation to the concept, boundary, or owner that gives it meaning.

Do not create five smaller `utils` files. The repair is ownership, not sharding.

## Why This Matters
Generic buckets quietly become dependency hubs because everyone is allowed to depend on them and they are allowed to contain almost anything.

That makes them attractive during implementation and expensive during evolution. A domain-neutral formatting helper may sit beside a database helper; soon the “shared” module imports infrastructure, then domain types, then configuration. Now lower layers cannot depend on it cleanly, higher layers all do, and extracting one piece requires untangling dependencies nobody consciously designed.

The cost is organizational too: the existence of the bucket trains contributors not to make ownership decisions. Every rushed change gets a default destination.

## Repair Strategy
Classify contents before moving them:

- domain rule → move to the domain/type/module whose invariant it expresses;
- boundary translation → move to the adapter/codec owning that boundary;
- infrastructure operation → move beside the resource/effect it controls;
- pure technical primitive with genuinely broad semantics → give it a narrow technical name and dependency policy;
- one-owner implementation detail → keep it local/private to that owner;
- duplicated code with no shared meaning → allow duplication rather than invent false common ownership.

Then fix imports so dependency direction follows the new ownership. Merely relocating a function while leaving a backward dependency into its old bucket does not complete the repair.

Prefer local helpers until a stable shared concept is proven by independent consumers. Extraction should follow semantic reuse, not precede it.

## Decision Branches
- **Helper encodes domain policy:** move it to that domain owner even if several callers use it.
- **Helper is a boundary codec/parser:** colocate with the boundary contract and tests.
- **Helper is genuinely generic and pure:** keep/extract it under a precise technical name; ban higher-level dependencies from leaking in.
- **Two domains have similar code but different reasons:** duplicate locally; similarity alone is not ownership.
- **Bucket is coherent but badly named:** rename it to the concept it already represents instead of gratuitous movement.
- **Moving a helper would create a cycle:** the cycle is evidence that current ownership/dependency direction needs redesign; do not use `utils` as a cycle escape hatch.

## Common Wrong Fixes
- Rename `utils` to `shared` or `common-core` and keep everything inside.
- Split by mechanical category (`stringUtils`, `objectUtils`, `miscUtils`) when the functions still encode unrelated domain rules.
- Create a global “platform” package that simply becomes the new junk drawer with more prestige.
- Extract after the second textual duplication without asking whether both sites depend on the same semantic law.
- Move helpers but re-export them from the old bucket forever, preserving both dependency paths.
- Add an ownership comment at the top while continuing to accept unrelated functions.

## Verification
After redistribution, each module should have an exclusion rule: a maintainer can explain not only what belongs there, but what does not.

Check dependency direction. Lower-level technical modules should not import high-level domain/infrastructure merely because a former helper did.

Search for the old bucket and remove obsolete re-exports/imports. Add architecture boundaries if the generic hub is likely to regrow.

Invariant:

> Shared code is shared because it has shared semantics, not because it lacked a home.

## Done When
There is no default drawer for ownerless code.

A new helper forces the author to answer “who owns this?” before the repository answers “put it in common.”
