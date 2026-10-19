# compatibility-cruft — Lesson

## Opinion

### Definition

Compatibility cruft is a second way of representing, calling, configuring, storing, or executing the same capability that survives without a concrete external obligation still requiring it.

It is architecture governed by an unnamed ghost consumer.

### Governing Principle

Compatibility is a debt instrument. It can be worth taking on, but the debt must have a creditor.

A real compatibility obligation names who would break, which old contract they still possess, how long overlap must exist, and what event permits removal. Without those facts, “backward compatibility” becomes a moral incantation that prevents simplification indefinitely.

The cost is not only extra code. Two live paths create two ontologies, two sets of tests, two failure modes, routing rules, migration ambiguity, and a permanent question in every future change: “which world am I changing?”

### Trigger When

Trigger when legacy aliases/adapters/formats/paths remain live primarily from unspecified fear rather than a named supported contract. Common forms:

- old and new API/tool names both remain after every repository-owned caller migrated;
- an old config key is accepted forever because “someone might still have it,” with no supported-version policy;
- dual read/write formats persist after migration, even though no durable old data or external producer still exists;
- compatibility adapters route between two internal models that are both repository-owned;
- a deprecated branch has no telemetry, consumer list, removal date/condition, or version boundary;
- “temporary” normalization accepts malformed historical shapes never observed in real data;
- new code must update both legacy and current representations to keep them synchronized;
- a clean-break product decision is undermined by hidden decode/alias fallbacks that preserve the old provider-facing ontology anyway.

### Do Not Trigger When

- A named external consumer/version still requires the old contract and removing it would violate a supported promise.
- Historical durable data genuinely requires old decode for recovery, while new writes use only the current format and legacy decode is quarantined at persistence ingress.
- A migration has an explicit overlap window, telemetry/consumer tracking, and concrete removal criterion.
- Standards/protocols require accepting multiple versions or representations as part of the actual current contract.
- Compatibility is itself the product requirement, not an implementation superstition.

### Distinguish From

`half-finished-refactor` concerns old/new ownership models both remaining authoritative inside the system. `compatibility-cruft` can exist even when ownership is clear, simply because obsolete external shapes are still accepted.

`legacy-cruft-retained` is broader historical debris. This rule specifically targets duplicate interfaces/representations justified as compatibility.

`guessed-migration` may create unnecessary compatibility because historical data was never inspected. Use that rule when the central failure is inventing a migration target; use this one when the duplicate path remains without a creditor.

### Decision Procedure

For each legacy path, demand four answers:

1. **Consumer:** who still uses it?
2. **Contract:** what supported promise requires it?
3. **Overlap:** why must old and new be live simultaneously?
4. **Exit:** what observable condition permits deletion?

If nobody can answer #1 or #2 concretely, the compatibility path is not protecting a contract. It is protecting anxiety.

If #1–#3 are real but #4 is missing, the migration has no mechanism for ever finishing.

### Examples

- positive: both `oldTool()` and `newTool()` remain indefinitely after all first-party callers moved; no external API exists.
- positive: decoder accepts three speculative legacy JSON shapes copied from old code comments, but no persisted sample or supported version contains them.
- positive: every write updates both v1 and v2 tables “for rollback safety” six months after rollback became impossible.
- near-miss: a public API supports clients on v1 for six months; usage telemetry and published deprecation date define the overlap.
- counterexample: current writes are v2-only, but recovery can still read real v1 durable records until the retention horizon expires.

## Advice

### What To Do Now

Make every compatibility path produce its papers.

Name the external consumer, the old contract it still holds, the required overlap, and the removal condition. Delete paths that cannot produce a real creditor. For legitimate migrations, quarantine compatibility at the boundary and make current internal code speak one ontology.

### Why This Matters

Compatibility code has a uniquely persuasive defense: deleting it might break someone you cannot see.

That possibility can justify real care. It can also make dead architecture immortal.

Every surviving alias, decoder, dual writer, fallback, legacy config key, and version branch adds a tax to future work. Engineers must understand and test behavior in both worlds. Bugs multiply at the reconciliation boundary. New designs become constrained by the least understood historical shape.

Eventually the current system is no longer designed for current users; it is designed around hypothetical users who may not exist.

### Repair Strategy

Inventory compatibility by contract, not by code location:

- identify each legacy name/shape/path;
- find actual consumers through public contracts, telemetry, repository search, durable data samples, support/version policy, or deployment inventory;
- classify whether compatibility is **external ingress**, **historical durable decode**, **rolling deployment overlap**, or **speculative internal fallback**;
- delete speculative internal fallback;
- for real external/historical cases, translate once into the current model at the boundary;
- make all new writes/emissions use the current form unless a real dual-write migration requires otherwise;
- encode an observable exit condition and owner.

Prefer asymmetric migrations: old may be read for a bounded reason; new should usually be written in one canonical form. Symmetric “support everything forever” guarantees the migration never converges.

### Decision Branches

- **No named consumer / no real old data:** delete the compatibility path and its tests.
- **External consumer still supported:** keep a narrow adapter, document/version the promise, and define deprecation/removal policy.
- **Historical durable data exists:** retain decode at persistence ingress only; do not let old representation leak into current domain code.
- **Rolling deployment needs overlap:** scope it to the deployment window and remove after fleet convergence is proven.
- **Rollback requires dual write temporarily:** define the rollback horizon and exact point after which the second write is removed.
- **Consumer cannot be identified because telemetry is absent:** add observation before assuming immortality. Lack of evidence is not evidence of a consumer.

### Common Wrong Fixes

- Rename a legacy path “compat” and declare the debt managed.
- Hide compatibility behind a facade while both ontologies remain active everywhere underneath.
- Add a generic normalization layer that accepts arbitrary historical shapes “just in case.”
- Keep dual writes forever because they are already implemented.
- Preserve every deprecated alias in types/tool schemas/provider-facing surfaces while claiming a clean break elsewhere.
- Delete real compatibility abruptly without checking named supported consumers. Anti-cruft is not permission to break contracts.
- Set a calendar removal date with no observable migration condition; dates alone do not prove consumers are gone.

### Verification

For every retained compatibility path, produce evidence for its creditor and exit condition.

For every removed path, verify:

- repository-owned callers use only current surface;
- public/supported consumers are not relying on the removed contract;
- durable historical data remains decodable if required;
- current writes/emissions no longer recreate the legacy form;
- tests no longer preserve obsolete ontology except at the explicit compatibility boundary.

Invariant:

> Current code has one canonical model; compatibility exists only at boundaries where a real supported past still touches the present.

### Done When

Every second path has a nameable reason to exist and a nameable reason it will stop existing.

If the only owner is “what if,” delete it.

### Nudge

Compatibility without a named consumer is fear with an API.

Name the creditor, name the exit, or delete the debt.
