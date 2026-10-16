# duplicated-control-flow — Lesson

## Opinion

### Definition

Control flow is duplicated when the same workflow, validation order, retry protocol, or state transition is independently re-expressed in more than one owner. The root-cause is that one protocol has several independent implementations and therefore several authorities over ordering and failure.

### Governing Principle

Duplication matters when it duplicates knowledge, not text. A workflow encodes temporal knowledge: which step precedes which, what failure cancels what, which result permits continuation. Copying that sequence creates two authorities over one protocol. They can remain textually similar while becoming semantically different one edit at a time.

### Trigger When

Trigger when multiple places independently implement the same ordered algorithm or transition protocol and changes to the rule must be synchronized manually.

### Do Not Trigger When

- Superficially similar sequences have independent reasons to change, failure semantics, or owners.
- Two loops share shape (map, filter, retry-once) but encode different domain protocols.
- A test restates production order as an observation, not as a second implementation of the protocol.
- Variation is already parameterized through one owner; callers are not re-implementing the sequence.

### Distinguish From

`duplicated-truth` concerns multiple authoritative representations of a fact. `premature-unification` warns against abstracting mere similarity. This rule applies when the repeated sequence is demonstrably one piece of knowledge. Tie-break: if one policy change to ordering or failure should be a single edit but currently is not, this rule owns the case.

### Decision Procedure

Ask whether a policy change to the sequence should require one edit or several coordinated edits. If one conceptual change demands several, establish a canonical owner.

### Examples

- positive: checkout and renewal each copy the same “validate → reserve → charge → confirm” protocol with slightly drifted failure handling.
- near-miss: two similar `for` loops over unrelated collections whose failure rules would never change together.
- counterexample: extract the shared protocol to one owner and route both callers through it.

## Advice

### What To Do Now

Move the genuinely shared workflow or transition protocol to one owner and make every caller invoke that canonical behavior. That canonical module is who owns the shared protocol; callers are not who owns a private copy of the same sequence.

### Why This Matters

Copied control flow duplicates time-sensitive knowledge: ordering, short-circuit rules, retry boundaries, and cleanup. Drift rarely appears as a dramatic fork; one branch gets a new condition, another misses it, and the system quietly acquires multiple definitions of the same process.

### Repair Strategy

Prove first that the sequences represent the same knowledge. Then extract the smallest operation that owns their common protocol, parameterizing only true variation rather than exposing the whole algorithm as callbacks.

### Decision Branches

- If a single policy change must be applied in several copies, extract one owner for the protocol.
- If the sequences only look alike, do not unify; coincidental shape is `premature-unification`.
- If the copies are facts rather than workflows, use `duplicated-truth` instead.

### Common Wrong Fixes

- Do not abstract merely because lines look alike.
- Do not create a generic orchestration framework whose parameters are more complex than the duplicated sequence.
- Do not leave one caller on the old copy “until later.”
- Do not extract only the happy path and leave failure/retry duplicated.

### Verification

A future change to ordering or failure semantics should have one authoritative edit point, with callers covered by behavior tests. The invariant is that one protocol has one implementation owner.

### Done When

One protocol has one implementation owner, and no caller can silently evolve a private version of the same workflow.

### Nudge

Do not copy a protocol. Give the shared ordering and failure semantics one canonical implementation, then route callers through that owner.
