# missing-invariant-documentation — Enforcer

## Definition
An invariant is undocumented when correctness depends on a non-obvious relation or forbidden state that exists only in implementation shape, memory, or tribal explanation rather than at the contract that owns it. The root-cause is that a falsifiable correctness relation has no durable statement at its semantic owner, so later edits must rediscover the rule from symptoms.

## Governing Principle
An invariant is knowledge that survives individual code paths: many implementations may preserve it, and many future changes can violate it. If the rule is not named at its owner, maintainers must rediscover it from symptoms or reverse-engineer it from defensive code. Hidden invariants are therefore delayed defects—correctness depends on knowledge the system has not made transmissible.

## Trigger When
Trigger when a material rule such as ordering, uniqueness, ownership, durability, or state relation is essential yet absent from authoritative documentation/types/tests and not obvious from local structure.

## Do Not Trigger When
- The invariant is already made explicit by a strong type or small clear contract whose meaning is mechanically evident and needs no redundant prose.
- The missing text is rationale for a choice rather than a correctness property (that is a decision record, not an invariant).
- The rule is a local implementation detail with no cross-path obligation.
- The missing text is a usage example or tutorial, not a correctness obligation that future changes must preserve.

## Distinguish From
`missing-architecture-gate` lacks enforcement for a known structural rule. `unrecorded-decision` lacks rationale for a choice. Tie-break: if the correctness property itself is not durably stated, this rule; if the rule is known but unenforced, `missing-architecture-gate`; if only why a choice was made is missing, `unrecorded-decision`.

## Decision Procedure
Name the invariant as a falsifiable sentence, locate its semantic owner, and record it there. Add mechanical enforcement when the property can be checked.

## Examples
- positive: Uniqueness of active leases is essential, yet lives only in a comment in one worker and nowhere on the lease contract.
- near-miss: A closed sum type already makes the forbidden combination unrepresentable, so extra prose would only restate the type.
- counterexample: The owning module documents the ordering rule and a property test fails when it is broken.

## Nudge
Hidden correctness is borrowed time. State the invariant where it is owned and let types, tests, or gates enforce as much of it as the machine can.
