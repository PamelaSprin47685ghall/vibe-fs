# duplicated-control-flow — Enforcer

## Definition
Control flow is duplicated when the same workflow, validation order, retry protocol, or state transition is independently re-expressed in more than one owner.

## Governing Principle
Duplication matters when it duplicates knowledge, not text. A workflow encodes temporal knowledge: which step precedes which, what failure cancels what, which result permits continuation. Copying that sequence creates two authorities over one protocol. They can remain textually similar while becoming semantically different one edit at a time.

## Trigger When
Trigger when multiple places independently implement the same ordered algorithm or transition protocol and changes to the rule must be synchronized manually.

## Do Not Trigger When
Do not trigger for superficially similar sequences whose reasons to change, failure semantics, or owners are genuinely independent.

## Distinguish From
duplicated-truth concerns multiple authoritative representations of a fact. premature-unification warns against abstracting mere similarity. This rule applies when the repeated sequence is demonstrably one piece of knowledge.

## Decision Procedure
Ask whether a policy change to the sequence should require one edit or several coordinated edits. If one conceptual change demands several, establish a canonical owner.

## Nudge
Do not copy a protocol. Give the shared ordering and failure semantics one canonical implementation, then route callers through that owner.
