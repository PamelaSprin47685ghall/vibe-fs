# boundary-collapse — Lesson

## Opinion

### Definition

A boundary has collapsed when two contexts that own different invariants can directly reach into one another’s representation, state, or lifecycle. The root-cause is that two contexts which own different invariants can reach each other’s representation or lifecycle, so private facts become accidental public obligations.

### Governing Principle

A boundary exists to make one side ignorant of facts it has no right to depend on. Once internals cross freely, each context acquires accidental knowledge of the other’s timing, storage, and representation. The system may still be split into files, but its change graph has become one object: any local revision can invalidate remote assumptions no interface records.

### Trigger When

Trigger when modules directly mutate each other’s state, import internal types, share a mutable model across contexts, or bypass an explicit translation/contract that should mediate the crossing.

### Do Not Trigger When

- The shared surface is itself the declared contract and both sides depend only on stable facts intentionally exported there.
- A context reads its own internals; no foreign owner is crossed.
- Translation at an explicit adapter copies only the contracted facts, not private representation.
- Test doubles sit behind the same declared interface production uses.

### Distinguish From

`cross-layer-internal-import` is a specific dependency violation. `context-model-leak` reuses one model across meanings. This rule is the broader loss of sovereignty between contexts. Tie-break: if either side can observe or mutate more than the declared crossing, this rule owns the case even when package layout still looks layered.

### Decision Procedure

Name each context’s invariant and lifecycle. Then list exactly which facts must cross. If either side can observe or change more than that list, restore the border.

### Examples

- positive: billing mutates an auth session object’s private fields, or imports an internal persistence type to “save a round trip.”
- near-miss: billing consumes a published `PaymentDue` DTO and never touches auth’s storage or lifecycle.
- counterexample: each context owns its model; an explicit translator copies only the contracted facts at the crossing.

## Advice

### What To Do Now

Re-establish an explicit interface between the contexts and reduce what crosses it to the smallest set of facts required by the consumer. Each context is who owns its private model and lifecycle; only an explicit translator at the declared crossing may copy contracted facts.

### Why This Matters

Architecture is a theory of permitted knowledge. When one context can depend on another’s internals, private implementation choices become public obligations by accident. The cost appears later as synchronized migrations, defensive adapters, and changes that cannot be reasoned about locally.

### Repair Strategy

Give each context ownership of its own model and invariants. Define the crossing in consumer-relevant terms, translate explicitly, and prevent internal imports or mutation mechanically where the language/build system permits.

### Decision Branches

- If one context mutates or imports another’s internals, restore a declared interface and cut the private reach.
- If a shared mutable model is the crossing, split ownership and translate facts instead of sharing representation.
- If the existing surface already exports only contracted facts, do not invent extra wrapping.

### Common Wrong Fixes

- Do not add a facade that forwards the same internals unchanged.
- Do not share a “common” mega-model to avoid translation.
- Do not move both contexts into one package so the import “looks local.”
- Do not document “please do not touch these fields” while leaving them reachable.

### Verification

A change to one context’s internal representation should not require edits in the other unless the declared cross-boundary fact itself changed. The invariant is that each context’s private representation and lifecycle remain invisible across the border.

### Done When

Each context can evolve behind its contract, cross-boundary data has an explicit owner and meaning, and no caller relies on another context’s private representation or lifecycle.

### Nudge

A boundary is not a folder line; it is a limit on knowledge. Export only the facts another context is entitled to know, and translate them explicitly at the crossing.
