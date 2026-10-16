# cross-layer-internal-import — Lesson

## Opinion

### Definition

A cross-layer internal import occurs when one layer depends on implementation members that another layer has not declared as part of its public contract. The root-cause is that a consumer treats another layer's private representation as if it were a supported contract.

### Governing Principle

Encapsulation is a promise about what may change independently. An internal import silently cancels that promise: private structure becomes a de facto API without review, versioning, or ownership. The dependency graph then understates the real architecture because source visibility, not declared design, determines who may know what.

### Trigger When

Trigger when higher, lower, or unrelated layers import internal modules, private paths, generated details, storage shapes, or implementation-only helpers across an intended boundary.

### Do Not Trigger When

- The imported surface is explicitly public, semantically stable, and owned as the supported contract between those layers.
- The import stays inside one module’s private implementation and never crosses a declared layer boundary.
- A white-box test owned by the same module inspects internals as part of that owner’s contract, not as a foreign layer dependency.
- Generated code is consumed only through the generated public entry the owner published as the contract, even if files live under a generated path.

### Distinguish From

`boundary-collapse` is the general erosion of context sovereignty. `cyclic-dependency` concerns graph direction. This rule is the concrete act of turning another layer’s private representation into your dependency. Tie-break: if the graph is still acyclic but a caller now depends on an undeclared internal member, this rule owns the case.

### Decision Procedure

Ask whether the provider can refactor the imported member without consulting this caller. If the architecture says yes but the code says no, the import crosses the boundary.

### Examples

- positive: an application service imports a persistence module’s table-layout helper that was never published as a contract.
- near-miss: the same service depends on a persistence repository port that the persistence owner declared public and stable.
- counterexample: move the needed fact into that declared public contract, or move the behavior to the layer that already owns the internal knowledge.

## Advice

### What To Do Now

Remove the internal import. Either expose the required fact through the provider’s deliberate public contract or relocate the behavior to the layer that already owns the necessary knowledge. The providing layer is who owns internal members; the consumer may depend only on the published contract.

### Why This Matters

Internal imports create obligations no architecture document records. A provider appears free to refactor but is not; a consumer appears dependent only on an interface but actually knows storage layout, helper structure, or lifecycle detail. This is coupling without an honest contract.

### Repair Strategy

Identify why the consumer reached inward. If it needs a stable fact, add the smallest public abstraction that owns that fact. If it needs implementation detail to perform policy, ownership is probably misplaced; move the policy instead.

### Decision Branches

- If the consumer needs a stable fact the provider already owns, publish the smallest public contract for that fact and delete the internal import.
- If the consumer needs implementation detail to enforce policy, move the policy to the owning layer rather than leaking internals outward.
- If the import is same-layer private wiring, leave it; this rule does not police intra-module structure.

### Common Wrong Fixes

- Do not re-export the same internal symbol under a public name without defining a stable semantic contract.
- Do not rename the import path or wrap it in a local alias; renaming leakage is still leakage.
- Do not copy the internal type into the consumer to “avoid importing”; that duplicates the private shape as a second undeclared contract.
- Do not mark the internal module public wholesale so the import becomes legal on paper.

### Verification

Architecture checks should fail on renewed internal imports. The invariant is that the provider’s internal layout can change without touching the consumer.

### Done When

Every cross-layer dependency points at an intentional contract whose owner accepts responsibility for its stability.

### Nudge

Depend on promises, not accidents of layout. Move the needed fact into a declared public contract or move the behavior to the layer that owns the internal knowledge.
