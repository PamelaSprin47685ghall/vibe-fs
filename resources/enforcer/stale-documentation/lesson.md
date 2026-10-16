# stale-documentation — Lesson

## Opinion

### Definition

Documentation is stale when an authoritative specification, schema, example, or diagram still describes a contract that implementation no longer obeys. The root-cause is that two surfaces both claim to describe the current contract but can change independently, so readers face an epistemic fork between prose and behavior.

### Governing Principle

Authoritative documentation and code are two representations of one agreement. If they can diverge independently, future design decisions may be logically correct relative to the wrong version of the contract. An authoritative document must therefore change atomically with the behavior it defines.

### Trigger When

Trigger when a code change alters a documented API, invariant, lifecycle, schema, command, or architecture while the owning documentation remains on the previous meaning.

### Do Not Trigger When

- The prose is clearly informal or historical and does not claim current authority.
- An established atomic generation or update process keeps the authoritative artifact synchronized in the same delivery.
- Only implementation comments that are not the owning contract drifted (prefer `comment-theater` if they fake structure).
- The change did not alter any documented contract, only private internals.

### Distinguish From

`comment-theater` concerns prose substituting for structure. `misleading-name` concerns identifiers. `missing-invariant-documentation` concerns an invariant that was never written down. This rule is disagreement between two surfaces that both claim to describe the current contract. Tie-break: if authoritative docs and behavior diverge, this rule owns the case.

### Decision Procedure

1. Locate every authoritative representation of the changed contract.
2. Ask whether a competent reader would reach a different conclusion than the new behavior.
3. If yes, update or remove that representation in the same change.
4. Leave explicitly historical text, and ensure current docs remain the authority.

### Examples

- positive: a command’s flags change in code while `docs/how` still documents the old CLI as current.
- near-miss: a dated `history.md` describes the old API and is labeled historical, not current.
- counterexample: schema, examples, and how-docs update in the same delivery as the behavior change.

## Advice

### What To Do Now

Update every authoritative document, schema, example, or diagram whose contract changed, in the same delivery as the implementation. The owning spec of that contract is who owns present-tense meaning; the implementation may not silently become a second current version.

### Why This Matters

Stale authoritative prose creates two competing truths. The next engineer may faithfully implement the written contract and thereby reintroduce the behavior the code had already replaced. Synchronization is therefore part of correctness, not clerical cleanup.

### Repair Strategy

Trace the changed concept to its owning documentation, update semantics rather than merely examples, and remove obsolete descriptions instead of leaving historical alternatives in current docs.

### Decision Branches

- If an owning spec, schema, or how-doc would predict the old behavior, update or remove it in this delivery.
- If the text is explicitly historical, leave it and ensure current docs are the authority.
- If comments rather than owning docs are theatrical, that is `comment-theater`—do not treat them as this rule’s primary fix.

### Common Wrong Fixes

- Do not add a note saying “docs may be outdated” or duplicate the new behavior in a second document.
- Do not update only a changelog while leaving the how or schema pages wrong.
- Do not fix examples but leave the stated invariant unchanged.
- Do not point readers at source “as the real docs” while still publishing an authoritative stale page.

### Verification

Read the documentation as though the implementation were unavailable. It should predict the observable contract exercised by tests and current code. The invariant is that every authoritative representation tells the same present-tense contract as the implementation.

### Done When

Every authoritative representation tells the same present-tense story, and a reader cannot follow maintained documentation into an obsolete API or invariant.

### Nudge

A contract cannot have two current versions. Change authoritative prose, schemas, examples, and diagrams with the behavior they govern so readers never have to guess which reality is newer.
