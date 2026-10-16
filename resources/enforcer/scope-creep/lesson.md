# scope-creep — Lesson

## Opinion

### Definition

Scope creep occurs when a change begins solving problems whose correction is not required by the stated outcome, governing invariant, or necessary consequences of the chosen design. The root-cause is that extra intent is attached to a finite proof, so necessity and opportunistic preference share one diff and causal attribution is lost.

### Governing Principle

A change is a proof with a boundary: it claims that a finite set of edits establishes a finite result. Unrelated cleanup enlarges both the proposition and the search space, making review less able to distinguish necessary consequences from preference. Restraint is therefore not conservatism; it preserves the chain from intent to modification.

### Trigger When

Trigger when implementation expands into unrelated redesign, cleanup, migrations, dependency changes, or behavior merely because the surrounding code is imperfect.

### Do Not Trigger When

- Adjacent changes are logically required to satisfy the acceptance criteria.
- An API change forces compilation or call-site updates that preserve behavior.
- The requested change directly disturbs an invariant that must be restored in the same delivery.
- Extra files are generated artifacts of the required edit, not independent redesign.

### Distinguish From

`wholesale-rewrite` chooses an unnecessarily broad replacement strategy for the same intent. `half-finished-refactor` stops a required migration midway. `big-batch-intent` fuses independent success conditions into one instruction before execution. This rule concerns work whose intent itself has expanded beyond justification. Tie-break: if extra unrelated intent is attached to this change, this rule owns the case.

### Decision Procedure

1. Name the stated outcome and the invariants it necessarily disturbs.
2. For each proposed edit, ask which acceptance criterion or necessary restore requires it.
3. If no direct chain exists, separate that work into another change.
4. Keep only transitive edits that compilation or the disturbed invariant demands.

### Examples

- positive: a bugfix for one parser also reformats an unrelated module and upgrades a dependency “while here.”
- near-miss: renaming a function requires updating every call site in the same change so the tree still compiles.
- counterexample: every material file in the diff maps to the stated acceptance criteria or a necessary invariant restore.

## Advice

### What To Do Now

Remove unrelated edits from the current delivery and keep only changes that follow directly from the requested outcome or an invariant that outcome necessarily disturbs. The stated acceptance criterion is who owns the change boundary; surrounding imperfection is not.

### Why This Matters

Broad changes make causality expensive. When behavior, cleanup, architecture, and migration move together, reviewers cannot tell which edit proves which requirement and regressions inherit a much larger suspect set. Small coherent scope is a reasoning optimization.

### Repair Strategy

Map each changed area to a requirement. Preserve necessary transitive edits, but defer independent improvements as separate work with their own acceptance criteria. Name the necessity of every kept adjacent file so the boundary remains inspectable.

### Decision Branches

- If an edit has no chain to the acceptance criteria or a disturbed invariant, move it out of this delivery.
- If an edit is required for compilation or invariant restore after the intended change, keep it and name that necessity.
- If the work has become a rewrite of an adjacent subsystem, split a new change rather than enlarge this one.

### Common Wrong Fixes

- Do not keep unrelated changes because they are individually good.
- Do not bundle drive-by formatting or dependency bumps to “reduce PR count.”
- Do not expand tests to cover untouched modules as a substitute for relevance.
- Do not leave the extra work in and add a paragraph of justification after the fact.

### Verification

The final diff should admit a concise explanation from every material edit back to the task’s contract, with no branch of work requiring a separate motivation. The invariant is that every material edit is a consequence of the stated outcome or a necessary restore of an invariant that outcome disturbs.

### Done When

The delivery is complete without being expansive: every edit participates in the same causal story, and unrelated opportunities remain separate rather than hidden inside it.

### Nudge

Keep one change answerable to one justified intent. Separate attractive but independent improvements so necessity, review, and rollback remain legible.
