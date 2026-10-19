# todo-bomb — Lesson

## Opinion

### Definition

A TODO becomes a correctness bomb when required behavior is replaced by a placeholder, unimplemented branch, panic, or note whose future completion is necessary for the shipped path to be sound. The root-cause is that a known hole on a reachable shipped path is converted into a deferred promise, so the current contract already depends on work that has not been done.

### Governing Principle

A TODO is not deferred work unless the current system remains complete without it. When correctness depends on the future task, the comment converts a known defect into an undocumented time bomb: execution may reach a state the implementation already knows it cannot honor. A system should either implement its contract or explicitly refuse the unsupported case at the boundary.

### Trigger When

Trigger when TODO/FIXME placeholders, temporary exceptions, `not implemented`, dummy returns, or panic branches stand in for behavior required by a reachable production contract.

### Do Not Trigger When

- Genuine backlog notes about optional future improvement that do not weaken correctness of any currently supported path.
- Explicit boundary rejection of an unsupported case with a typed outcome, even if a later product increment may add it.
- TODOs in tests or docs describing future coverage that is not claimed as shipped behavior.
- Scaffolding in an unmerged spike branch that cannot reach production.

### Distinguish From

`half-finished-refactor` leaves migration incomplete. `spike-not-cleaned` ships prototype shortcuts. Tie-break: if known required correctness is postponed inside delivered code via a placeholder, use this rule; if a prototype shortcut shipped without cleanup, use `spike-not-cleaned`.

### Decision Procedure

Ask whether a valid supported input can reach the placeholder today. If yes, either implement the behavior or narrow the supported contract so the case is explicitly and intentionally rejected.

### Examples

- positive: a reachable production branch returns a dummy value with `// TODO: compute tax`.
- near-miss: a backlog comment on an optional report the product does not yet offer, and no supported path enters that code.
- counterexample: a merged spike still using a hardcoded table “for now” is `spike-not-cleaned`.

## Advice

### What To Do Now

Complete the correctness-critical behavior or explicitly remove that case from the supported contract before delivery. The shipped contract is who owns required behavior; a TODO cannot fulfill that ownership.

### Why This Matters

A reachable TODO is a known hole hidden behind future tense. The code already knows a valid path can arrive somewhere it cannot honor, yet shifts the cost to whichever user or maintainer encounters it first.

### Repair Strategy

Trace whether the placeholder is reachable from supported inputs. Implement the real behavior and test it, or move rejection to the public boundary with a typed unsupported outcome if the product intentionally does not support the case.

### Decision Branches

If a supported input can reach the placeholder, implement the behavior or reject that case at the public boundary.
If the note is optional future work on an unsupported path, leave it as backlog and keep it unreachable.

### Common Wrong Fixes

- Replace `TODO` with a quieter default, empty result, or generic catch.
- Comment out the incomplete branch so the path silently skips required work.
- Ship the placeholder and file a ticket as if the ticket fulfilled the contract.

### Verification

Invariant: every reachable supported case has defined behavior and a test. Intentionally unsupported cases must be rejected explicitly before entering an incomplete path.

### Done When

No production path relies on a promise that only a comment makes about future work.

### Nudge

A comment cannot fulfill a contract. Finish required behavior before delivery, or make the unsupported case an explicit boundary rather than a hidden promise to the future.
