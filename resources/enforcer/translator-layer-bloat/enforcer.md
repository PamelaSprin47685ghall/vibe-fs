# translator-layer-bloat — Enforcer

## Definition
A translator layer is bloated when a broker, coordinator, manager, adapter, governor, or mediator exists chiefly to forward calls and rename values without owning a semantic transformation or invariant.

## Governing Principle
A layer earns existence by changing the terms under which reasoning is valid: translating protocols, enforcing authorization, normalizing representation, batching, isolating failure, or owning policy. Pure forwarding adds another place to search and another vocabulary to learn while preserving the same knowledge on both sides. Indirection without information hiding is distance, not abstraction.

## Trigger When
Trigger when a layer mostly delegates method-for-method, mirrors DTOs, or passes values through while enforcing no distinct contract, lifecycle, or transformation.

## Do Not Trigger When
- The layer performs a real semantic boundary: validation, auth, protocol mapping, transaction ownership, batching, anti-corruption translation, or another invariant absent from both neighbors.
- Generated protocol stubs that are the actual wire contract, not an extra rename hop.
- A thin adapter that owns failure isolation, retries, or resource lifetime even when method names look similar.
- An anti-corruption layer that changes identifiers, units, or error algebra between bounded contexts.

## Distinguish From
`facade-hides-mess` gives a clean surface over unresolved internals. `generic-helper-bucket` has no conceptual owner. Tie-break: if the intermediate layer’s only behavior is forwarding ceremony, use this rule; if a clean facade conceals an unresolved mess underneath, use `facade-hides-mess`.

## Decision Procedure
Remove the layer mentally and connect its neighbors. If no invariant, information boundary, or failure model is lost, delete it; otherwise name that invariant and make the layer visibly own it.

## Examples
- positive: `UserManager` delegates every method to `UserService` with identical DTOs and no extra policy.
- near-miss: an anti-corruption adapter maps external IDs, units, and errors into the domain model.
- counterexample: a tidy public API over a still-tangled core is `facade-hides-mess`.

## Nudge
Indirection must buy a boundary. Remove layers that only relay calls, or give them a genuine semantic transformation that justifies making callers cross them.
