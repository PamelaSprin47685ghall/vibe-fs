# dead-code-delivered — Lesson

## Opinion

### Definition

Delivered dead code is production code with no reachable role in the current system: unused, superseded, unreferenced, or impossible to execute. The root-cause is that the working tree still presents abandoned paths as live system, while history already owns the archive.

### Governing Principle

Version control is the archive; the working tree is the proposition “this is the system.” Dead code weakens that proposition by preserving alternatives that no longer participate in behavior. Readers must still reason about them, tools still index them, and future maintainers cannot tell whether the path is intentionally dormant or merely forgotten.

### Trigger When

Trigger when production functions, branches, modules, aliases, or paths are unreachable or have no remaining caller after a change.

### Do Not Trigger When

- The path is an intentionally dormant extension point whose activation contract and owner are explicit and tested.
- The code is a supported compatibility surface with a named owner and a documented retirement plan, not an abandoned leftover.
- A feature flag still has a live activation contract, tests, and an owner; the off path is dormant by design, not forgotten.
- Tests, fixtures, or generated snapshots exist only to prove current behavior and are themselves referenced by the suite.

### Distinguish From

`commented-out-code` stores old code in comments. `legacy-cruft-retained` keeps obsolete compatibility surfaces. This rule concerns executable source that no longer belongs to any current behavior. Tie-break: if the source still compiles and could run but has no present caller or contract, this rule owns the case.

### Decision Procedure

Find the caller, activation condition, or contract that gives the code a present role. If none exists, delete it rather than preserve hypothetical utility.

### Examples

- positive: a production helper remains after its last caller was removed; searches still surface it as if it were live.
- near-miss: a plugin hook with an explicit owner, activation test, and documented off-by-default contract.
- counterexample: delete the unreachable path and let version control keep history.

## Advice

### What To Do Now

Delete production code that has no current caller, activation path, or contractual role. Version control is who owns history; the working tree is who owns only paths with a present role.

### Why This Matters

Dead code has zero runtime value but nonzero reasoning cost. It creates false alternatives in the reader’s model and makes searches, refactors, security review, and ownership analysis less trustworthy. Keeping it “for later” transfers uncertainty to every future maintainer.

### Repair Strategy

Trace references and activation paths, remove the dead surface, then clean imports, tests, flags, and documentation that existed only to support it. Preserve historical availability in version control rather than the source tree.

### Decision Branches

- If no caller, activation path, or contract remains, delete the production path and its exclusive support files.
- If an owner still claims the path as a tested extension or compatibility surface, keep it only with that explicit contract.
- If the path is commented out rather than executable, this is not this rule; restore or delete under the comment rule.

### Common Wrong Fixes

- Do not comment the dead path out; comments are not an archive.
- Do not hide it behind an always-false flag or unreachable branch.
- Do not move it to a “legacy” folder that still ships.
- Do not keep it “for later” because version control already stores history.

### Verification

Build and test after removal. Search for references to the retired names and ensure no documented supported behavior depended on them. The invariant is that every remaining production path has a present reason to exist.

### Done When

Every production path has a current reason to exist, and the source tree no longer asks readers to distinguish the live system from its abandoned possibilities.

### Nudge

Keep only code with a present proof of life. Delete unreachable production paths; history can recover them if the future ever needs them again.
