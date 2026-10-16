# legacy-cruft-retained — Lesson

## Opinion

### Definition

Legacy cruft is retained when obsolete aliases, names, formats, branches, or code paths survive despite a deliberate clean-break decision that removed any compatibility obligation.

### Governing Principle

A clean break is valuable because it collapses the state space: after the boundary, the old world is no longer a world the system must interpret. Retaining legacy paths nullifies that benefit. The project pays both histories forever while pretending the migration is complete, and future developers cannot know whether the old path is forbidden, tolerated, or secretly required.

### Trigger When

Trigger when an explicit clean-break policy exists yet obsolete code, aliases, compatibility branches, old names, or legacy formats remain reachable.

### Do Not Trigger When

- Do not trigger when a current external contract explicitly requires the old surface for a bounded migration period.
- Do not trigger when the remaining old name appears only in changelog or docs describing the break.
- Do not trigger for on-disk residue that current readers reject (unreadable leftover, not a live path).

### Distinguish From

compatibility-cruft lacks a justified external requirement. half-finished-refactor leaves migration incomplete. This rule is sharper: a decision already said the old world should cease to exist. Tie-break: if no clean break was decided and dual owners remain, use half-finished-refactor; if the break was decided and the old surface still lives, use this rule.

### Decision Procedure

Find the clean-break decision and identify every remaining representation of the retired surface. The root-cause is a ghost contract after the break already retired the old world; if no authorized exception exists, remove all of them rather than re-litigating the decision in code. Prefer this over incomplete migration when the decision already said the old surface must cease.

### Examples

- positive: A v1 parser and alias remain reachable after the project declared v2 the only supported format.
- near-miss: An external client still needs v1 until a dated retirement; that adapter is the only remaining old surface.
- counterexample: The old surface is deleted; git history remembers it.

## Advice

### What To Do Now

Honor the clean-break at the root-cause owner: who owns the retired surface must delete aliases, adapters, old names, compatibility branches, and legacy formats rather than keep a ghost path.

### Why This Matters

A migration that keeps its predecessor alive has not reduced complexity; it has merely renamed one side “legacy.” Every surviving branch remains a supported possibility until code makes it impossible, so tests, docs, and future refactors must keep asking whether the old world still matters.

### Repair Strategy

Use the clean-break decision as authority, migrate any remaining repository-owned callers, and remove the old representation completely. Record only explicit external exceptions with a retirement condition.

### Decision Branches

- If the clean break has no authorized exception, delete every reachable old surface.
- If an external contract still requires the old path, keep only that exception with a retirement condition.

### Common Wrong Fixes

- Do not keep a hidden alias “for safety,” a deprecated parser “just in case,” or commented guidance for the old path. Those reintroduce ambiguity the clean break was meant to eliminate.
- Do not mark the path deprecated while leaving it on the default execution route.
- Do not re-enable the old format in tests “for coverage” after production forbids it.

### Verification

Search for old names and formats, exercise the canonical path, and ensure architecture/tests contain no fallback that silently accepts the retired surface. The invariant: the post-break world is the only supported world in production code.

### Done When

The codebase supports the post-break world only, and version control—not production code—carries the memory of what came before.

### Nudge

A clean break should reduce the number of supported worlds to one. Honor the decision: remove the legacy surface instead of carrying a ghost contract forward.
