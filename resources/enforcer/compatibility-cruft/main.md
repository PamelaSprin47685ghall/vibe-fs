# compatibility-cruft — Main

## What To Do Now
Make every compatibility path produce its papers.

Name the external consumer, the old contract it still holds, the required overlap, and the removal condition. Delete paths that cannot produce a real creditor. For legitimate migrations, quarantine compatibility at the boundary and make current internal code speak one ontology.

## Why This Matters
Compatibility code has a uniquely persuasive defense: deleting it might break someone you cannot see.

That possibility can justify real care. It can also make dead architecture immortal.

Every surviving alias, decoder, dual writer, fallback, legacy config key, and version branch adds a tax to future work. Engineers must understand and test behavior in both worlds. Bugs multiply at the reconciliation boundary. New designs become constrained by the least understood historical shape.

Eventually the current system is no longer designed for current users; it is designed around hypothetical users who may not exist.

## Repair Strategy
Inventory compatibility by contract, not by code location:

- identify each legacy name/shape/path;
- find actual consumers through public contracts, telemetry, repository search, durable data samples, support/version policy, or deployment inventory;
- classify whether compatibility is **external ingress**, **historical durable decode**, **rolling deployment overlap**, or **speculative internal fallback**;
- delete speculative internal fallback;
- for real external/historical cases, translate once into the current model at the boundary;
- make all new writes/emissions use the current form unless a real dual-write migration requires otherwise;
- encode an observable exit condition and owner.

Prefer asymmetric migrations: old may be read for a bounded reason; new should usually be written in one canonical form. Symmetric “support everything forever” guarantees the migration never converges.

## Decision Branches
- **No named consumer / no real old data:** delete the compatibility path and its tests.
- **External consumer still supported:** keep a narrow adapter, document/version the promise, and define deprecation/removal policy.
- **Historical durable data exists:** retain decode at persistence ingress only; do not let old representation leak into current domain code.
- **Rolling deployment needs overlap:** scope it to the deployment window and remove after fleet convergence is proven.
- **Rollback requires dual write temporarily:** define the rollback horizon and exact point after which the second write is removed.
- **Consumer cannot be identified because telemetry is absent:** add observation before assuming immortality. Lack of evidence is not evidence of a consumer.

## Common Wrong Fixes
- Rename a legacy path “compat” and declare the debt managed.
- Hide compatibility behind a facade while both ontologies remain active everywhere underneath.
- Add a generic normalization layer that accepts arbitrary historical shapes “just in case.”
- Keep dual writes forever because they are already implemented.
- Preserve every deprecated alias in types/tool schemas/provider-facing surfaces while claiming a clean break elsewhere.
- Delete real compatibility abruptly without checking named supported consumers. Anti-cruft is not permission to break contracts.
- Set a calendar removal date with no observable migration condition; dates alone do not prove consumers are gone.

## Verification
For every retained compatibility path, produce evidence for its creditor and exit condition.

For every removed path, verify:

- repository-owned callers use only current surface;
- public/supported consumers are not relying on the removed contract;
- durable historical data remains decodable if required;
- current writes/emissions no longer recreate the legacy form;
- tests no longer preserve obsolete ontology except at the explicit compatibility boundary.

Invariant:

> Current code has one canonical model; compatibility exists only at boundaries where a real supported past still touches the present.

## Done When
Every second path has a nameable reason to exist and a nameable reason it will stop existing.

If the only owner is “what if,” delete it.
