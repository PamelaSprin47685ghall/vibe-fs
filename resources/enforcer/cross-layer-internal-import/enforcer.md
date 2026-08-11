# cross-layer-internal-import — Enforcer

## Definition
A cross-layer internal import occurs when one layer depends on implementation members that another layer has not declared as part of its public contract.

## Governing Principle
Encapsulation is a promise about what may change independently. An internal import silently cancels that promise: private structure becomes a de facto API without review, versioning, or ownership. The dependency graph then understates the real architecture because source visibility, not declared design, determines who may know what.

## Trigger When
Trigger when higher, lower, or unrelated layers import internal modules, private paths, generated details, storage shapes, or implementation-only helpers across an intended boundary.

## Do Not Trigger When
- The imported surface is explicitly public, semantically stable, and owned as the supported contract between those layers.
- The import stays inside one module’s private implementation and never crosses a declared layer boundary.
- A white-box test owned by the same module inspects internals as part of that owner’s contract, not as a foreign layer dependency.
- Generated code is consumed only through the generated public entry the owner published as the contract, even if files live under a generated path.

## Distinguish From
`boundary-collapse` is the general erosion of context sovereignty. `cyclic-dependency` concerns graph direction. This rule is the concrete act of turning another layer’s private representation into your dependency. Tie-break: if the graph is still acyclic but a caller now depends on an undeclared internal member, this rule owns the case.

## Decision Procedure
Ask whether the provider can refactor the imported member without consulting this caller. If the architecture says yes but the code says no, the import crosses the boundary.

## Examples
- positive: an application service imports a persistence module’s table-layout helper that was never published as a contract.
- near-miss: the same service depends on a persistence repository port that the persistence owner declared public and stable.
- counterexample: move the needed fact into that declared public contract, or move the behavior to the layer that already owns the internal knowledge.

## Nudge
Depend on promises, not accidents of layout. Move the needed fact into a declared public contract or move the behavior to the layer that owns the internal knowledge.
