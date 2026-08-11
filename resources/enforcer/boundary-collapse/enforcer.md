# boundary-collapse — Enforcer

## Definition
A boundary has collapsed when two contexts that own different invariants can directly reach into one another’s representation, state, or lifecycle.

## Governing Principle
A boundary exists to make one side ignorant of facts it has no right to depend on. Once internals cross freely, each context acquires accidental knowledge of the other’s timing, storage, and representation. The system may still be split into files, but its change graph has become one object: any local revision can invalidate remote assumptions no interface records.

## Trigger When
Trigger when modules directly mutate each other’s state, import internal types, share a mutable model across contexts, or bypass an explicit translation/contract that should mediate the crossing.

## Do Not Trigger When
- The shared surface is itself the declared contract and both sides depend only on stable facts intentionally exported there.
- A context reads its own internals; no foreign owner is crossed.
- Translation at an explicit adapter copies only the contracted facts, not private representation.
- Test doubles sit behind the same declared interface production uses.

## Distinguish From
`cross-layer-internal-import` is a specific dependency violation. `context-model-leak` reuses one model across meanings. This rule is the broader loss of sovereignty between contexts. Tie-break: if either side can observe or mutate more than the declared crossing, this rule owns the case even when package layout still looks layered.

## Decision Procedure
Name each context’s invariant and lifecycle. Then list exactly which facts must cross. If either side can observe or change more than that list, restore the border.

## Examples
- positive: billing mutates an auth session object’s private fields, or imports an internal persistence type to “save a round trip.”
- near-miss: billing consumes a published `PaymentDue` DTO and never touches auth’s storage or lifecycle.
- counterexample: each context owns its model; an explicit translator copies only the contracted facts at the crossing.

## Nudge
A boundary is not a folder line; it is a limit on knowledge. Export only the facts another context is entitled to know, and translate them explicitly at the crossing.
