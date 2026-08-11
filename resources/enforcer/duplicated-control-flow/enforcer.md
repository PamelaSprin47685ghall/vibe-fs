# duplicated-control-flow — Enforcer

## Definition
Control flow is duplicated when the same workflow, validation order, retry protocol, or state transition is independently re-expressed in more than one owner. The root-cause is that one protocol has several independent implementations and therefore several authorities over ordering and failure.

## Governing Principle
Duplication matters when it duplicates knowledge, not text. A workflow encodes temporal knowledge: which step precedes which, what failure cancels what, which result permits continuation. Copying that sequence creates two authorities over one protocol. They can remain textually similar while becoming semantically different one edit at a time.

## Trigger When
Trigger when multiple places independently implement the same ordered algorithm or transition protocol and changes to the rule must be synchronized manually.

## Do Not Trigger When
- Superficially similar sequences have independent reasons to change, failure semantics, or owners.
- Two loops share shape (map, filter, retry-once) but encode different domain protocols.
- A test restates production order as an observation, not as a second implementation of the protocol.
- Variation is already parameterized through one owner; callers are not re-implementing the sequence.

## Distinguish From
`duplicated-truth` concerns multiple authoritative representations of a fact. `premature-unification` warns against abstracting mere similarity. This rule applies when the repeated sequence is demonstrably one piece of knowledge. Tie-break: if one policy change to ordering or failure should be a single edit but currently is not, this rule owns the case.

## Decision Procedure
Ask whether a policy change to the sequence should require one edit or several coordinated edits. If one conceptual change demands several, establish a canonical owner.

## Examples
- positive: checkout and renewal each copy the same “validate → reserve → charge → confirm” protocol with slightly drifted failure handling.
- near-miss: two similar `for` loops over unrelated collections whose failure rules would never change together.
- counterexample: extract the shared protocol to one owner and route both callers through it.

## Nudge
Do not copy a protocol. Give the shared ordering and failure semantics one canonical implementation, then route callers through that owner.
