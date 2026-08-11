# cross-layer-internal-import — Enforcer

## Definition
A cross-layer internal import occurs when one layer depends on implementation members that another layer has not declared as part of its public contract.

## Governing Principle
Encapsulation is a promise about what may change independently. An internal import silently cancels that promise: private structure becomes a de facto API without review, versioning, or ownership. The dependency graph then understates the real architecture because source visibility, not declared design, determines who may know what.

## Trigger When
Trigger when higher, lower, or unrelated layers import internal modules, private paths, generated details, storage shapes, or implementation-only helpers across an intended boundary.

## Do Not Trigger When
Do not trigger when the imported surface is explicitly public, semantically stable, and owned as the supported contract between those layers.

## Distinguish From
boundary-collapse is the general erosion of context sovereignty. cyclic-dependency concerns graph direction. This rule is the concrete act of turning another layer’s private representation into your dependency.

## Decision Procedure
Ask whether the provider can refactor the imported member without consulting this caller. If the architecture says yes but the code says no, the import crosses the boundary.

## Nudge
Depend on promises, not accidents of layout. Move the needed fact into a declared public contract or move the behavior to the layer that owns the internal knowledge.
