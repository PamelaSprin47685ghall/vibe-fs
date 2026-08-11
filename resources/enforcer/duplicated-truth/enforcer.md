# duplicated-truth — Enforcer

## Definition
Truth is duplicated when the same fact has more than one representation that may be independently written and each is treated as authoritative. The root-cause is that one present fact has more than one writable authority.

## Governing Principle
A fact can have many projections but only one authority. Once two writable representations claim equal status, disagreement becomes a legal system state and every read must answer a new question: which copy wins? Synchronization code cannot eliminate this problem; it merely defines increasingly elaborate rituals for repairing the contradiction after the model allowed it.

## Trigger When
Trigger when one fact is stored, configured, cached, or encoded in multiple independently mutable places with no strict derivation relation.

## Do Not Trigger When
- Secondary representations are read-only projections that can be rebuilt deterministically from a declared source of truth.
- A cache is explicitly disposable and always derived; a miss never invents a competing write.
- Display copies, logs, or API views cannot write back into the fact.
- Two different facts happen to share a similar shape; they are not one truth.

## Distinguish From
`overwrite-history` changes prior facts. `snapshot-as-truth` promotes a projection to authority. This rule concerns simultaneous authorities for one present fact. Tie-break: if two writable copies of the same present fact can disagree, this rule owns the case.

## Decision Procedure
For any disagreement, ask which representation the system is obligated to believe. If the answer is ambiguous or context-dependent, choose one authority and make the others derived.

## Examples
- positive: user email is writable in both the profile table and a billing config blob, and either may be treated as current.
- near-miss: a read-only search index rebuilt from the profile table, never written independently.
- counterexample: keep one writable source and derive every other representation from it.

## Nudge
Many views are fine; many authorities are not. Choose one writable truth and derive every other representation from it.
