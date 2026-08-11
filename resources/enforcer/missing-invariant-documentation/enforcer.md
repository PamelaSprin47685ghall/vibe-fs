# missing-invariant-documentation — Enforcer

## Definition
An invariant is undocumented when correctness depends on a non-obvious relation or forbidden state that exists only in implementation shape, memory, or tribal explanation rather than at the contract that owns it.

## Governing Principle
An invariant is knowledge that survives individual code paths: many implementations may preserve it, and many future changes can violate it. If the rule is not named at its owner, maintainers must rediscover it from symptoms or reverse-engineer it from defensive code. Hidden invariants are therefore delayed defects—correctness depends on knowledge the system has not made transmissible.

## Trigger When
Trigger when a material rule such as ordering, uniqueness, ownership, durability, or state relation is essential yet absent from authoritative documentation/types/tests and not obvious from local structure.

## Do Not Trigger When
Do not trigger when the invariant is already made explicit by a strong type or small clear contract whose meaning is mechanically evident and needs no redundant prose.

## Distinguish From
missing-architecture-gate lacks enforcement for a known structural rule. unrecorded-decision lacks rationale for a choice. This rule concerns a correctness property whose existence itself is not durably stated.

## Decision Procedure
Name the invariant as a falsifiable sentence, locate its semantic owner, and record it there. Add mechanical enforcement when the property can be checked.

## Nudge
Hidden correctness is borrowed time. State the invariant where it is owned and let types, tests, or gates enforce as much of it as the machine can.
