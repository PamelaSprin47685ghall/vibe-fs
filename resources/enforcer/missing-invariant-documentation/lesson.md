# missing-invariant-documentation — Lesson

## Opinion

### Definition

An invariant is undocumented when correctness depends on a non-obvious relation or forbidden state that exists only in implementation shape, memory, or tribal explanation rather than at the contract that owns it. The root-cause is that a falsifiable correctness relation has no durable statement at its semantic owner, so later edits must rediscover the rule from symptoms.

### Governing Principle

An invariant is knowledge that survives individual code paths: many implementations may preserve it, and many future changes can violate it. If the rule is not named at its owner, maintainers must rediscover it from symptoms or reverse-engineer it from defensive code. Hidden invariants are therefore delayed defects—correctness depends on knowledge the system has not made transmissible.

### Trigger When

Trigger when a material rule such as ordering, uniqueness, ownership, durability, or state relation is essential yet absent from authoritative documentation/types/tests and not obvious from local structure.

### Do Not Trigger When

- The invariant is already made explicit by a strong type or small clear contract whose meaning is mechanically evident and needs no redundant prose.
- The missing text is rationale for a choice rather than a correctness property (that is a decision record, not an invariant).
- The rule is a local implementation detail with no cross-path obligation.
- The missing text is a usage example or tutorial, not a correctness obligation that future changes must preserve.

### Distinguish From

`missing-architecture-gate` lacks enforcement for a known structural rule. `unrecorded-decision` lacks rationale for a choice. Tie-break: if the correctness property itself is not durably stated, this rule; if the rule is known but unenforced, `missing-architecture-gate`; if only why a choice was made is missing, `unrecorded-decision`.

### Decision Procedure

Name the invariant as a falsifiable sentence, locate its semantic owner, and record it there. Add mechanical enforcement when the property can be checked.

### Examples

- positive: Uniqueness of active leases is essential, yet lives only in a comment in one worker and nowhere on the lease contract.
- near-miss: A closed sum type already makes the forbidden combination unrepresentable, so extra prose would only restate the type.
- counterexample: The owning module documents the ordering rule and a property test fails when it is broken.

## Advice

### What To Do Now

Write the hidden correctness rule as a precise invariant at the contract that owns it, then encode a type, test, or gate wherever practical. The owning contract is who owns the invariant: one falsifiable sentence there, not scattered comments near symptoms.

### Why This Matters

Undocumented invariants survive only inside people who have already paid the cost to discover them. When those people leave or context fades, later changes can violate the rule while appearing locally reasonable. The defect is not lack of prose; it is failure to preserve necessary knowledge.

### Repair Strategy

State what must always be true, what boundary owns the rule, and what evidence would falsify it. Keep prose concise, then move enforceable parts into construction, exhaustive matching, architecture checks, or behavioral tests.

### Decision Branches

- If the property can be made unrepresentable by a type or construction rule, encode it there and keep a one-sentence owner statement.
- If it cannot be typed, record the falsifiable sentence at the owner and add the cheapest test or gate that detects violation.

### Common Wrong Fixes

- Scatter the same rule across comments near symptoms instead of one authoritative definition.
- Write vague prose ("be careful with ordering") that cannot be falsified.
- Duplicate the invariant in several modules so later edits diverge.

### Verification

A new contributor should be able to locate the invariant from the owning concept and identify the mechanism that prevents or detects violation.

### Done When

Critical correctness no longer depends on oral tradition: the rule is named once, owned clearly, and mechanically guarded where possible.

### Nudge

Hidden correctness is borrowed time. State the invariant where it is owned and let types, tests, or gates enforce as much of it as the machine can.
