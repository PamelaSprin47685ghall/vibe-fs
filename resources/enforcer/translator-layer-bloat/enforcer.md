# translator-layer-bloat — Enforcer

## Definition
A translator layer is bloated when a broker, coordinator, manager, adapter, governor, or mediator exists chiefly to forward calls and rename values without owning a semantic transformation or invariant.

## Governing Principle
A layer earns existence by changing the terms under which reasoning is valid: translating protocols, enforcing authorization, normalizing representation, batching, isolating failure, or owning policy. Pure forwarding adds another place to search and another vocabulary to learn while preserving the same knowledge on both sides. Indirection without information hiding is distance, not abstraction.

## Trigger When
Trigger when a layer mostly delegates method-for-method, mirrors DTOs, or passes values through while enforcing no distinct contract, lifecycle, or transformation.

## Do Not Trigger When
Do not trigger when the layer performs a real semantic boundary: validation, auth, protocol mapping, transaction ownership, batching, anti-corruption translation, or another invariant absent from both neighbors.

## Distinguish From
facade-hides-mess gives a clean surface over unresolved internals. generic-helper-bucket has no conceptual owner. This rule concerns an intermediate layer whose only behavior is forwarding ceremony.

## Decision Procedure
Remove the layer mentally and connect its neighbors. If no invariant, information boundary, or failure model is lost, delete it; otherwise name that invariant and make the layer visibly own it.

## Nudge
Indirection must buy a boundary. Remove layers that only relay calls, or give them a genuine semantic transformation that justifies making callers cross them.
