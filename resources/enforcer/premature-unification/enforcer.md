# premature-unification — Enforcer

## Definition
Unification is premature when similar-looking code or data with different invariants, lifecycles, or reasons to change is forced behind one abstraction before it represents one piece of knowledge.

## Governing Principle
DRY applies to knowledge, not appearance. Two structures may be textually identical today because independent domains happen to share a shape. Unifying them creates a new claim: they must evolve together. If that claim is false, every future divergence fights the abstraction through flags, optional fields, hooks, or special cases until the "reuse" costs more than the duplication it removed.

## Trigger When
Trigger when concepts are merged primarily because their fields, functions, or workflow steps look alike while their owners and change causes remain independent.

## Do Not Trigger When
- A genuine shared invariant has emerged and a change to that knowledge should consistently affect all consumers.
- The duplication is the same protocol, not coincidental shape.
- The abstraction is a tiny shared primitive with no claim that the domains evolve together.

## Distinguish From
duplicated-control-flow applies when repeated code really is one protocol. context-model-leak reuses one model across meanings. Tie-break: if unification happened before sameness of knowledge was established, this rule; if the repeated flow truly is one protocol, duplicated-control-flow; if one model is stretched across contexts, context-model-leak.

## Decision Procedure
Ask: if one concept changes for a domain reason, must the other change too? If not, keep them separate even if their implementation is currently identical.

## Examples
- positive: `UserAddress` and `ShippingAddress` are forced into one type, then billing-only fields appear as optionals.
- near-miss: Two call sites share one validation law that must stay identical; extracting that law is real DRY.
- counterexample: Each concept has its own type; similar helpers remain duplicated until a shared invariant appears.

## Nudge
Similarity is evidence of resemblance, not shared ownership. Abstract only when the repeated thing is one fact that must evolve as one fact.
