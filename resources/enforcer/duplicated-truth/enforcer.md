# duplicated-truth — Enforcer

## Definition
Truth is duplicated when the same fact has more than one representation that may be independently written and each is treated as authoritative.

## Governing Principle
A fact can have many projections but only one authority. Once two writable representations claim equal status, disagreement becomes a legal system state and every read must answer a new question: which copy wins? Synchronization code cannot eliminate this problem; it merely defines increasingly elaborate rituals for repairing the contradiction after the model allowed it.

## Trigger When
Trigger when one fact is stored, configured, cached, or encoded in multiple independently mutable places with no strict derivation relation.

## Do Not Trigger When
Do not trigger when secondary representations are read-only projections that can be rebuilt deterministically from a declared source of truth.

## Distinguish From
overwrite-history changes prior facts. snapshot-as-truth promotes a projection to authority. This rule concerns simultaneous authorities for one present fact.

## Decision Procedure
For any disagreement, ask which representation the system is obligated to believe. If the answer is ambiguous or context-dependent, choose one authority and make the others derived.

## Nudge
Many views are fine; many authorities are not. Choose one writable truth and derive every other representation from it.
