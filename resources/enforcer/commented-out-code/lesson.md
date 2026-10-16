# commented-out-code — Lesson

## Opinion

### Definition

Commented-out implementation is dead code kept inside the living source as an informal archive. The root-cause is that former implementation is warehoused in comments, so present-tense source and historical residue share one visual channel.

### Governing Principle

Source should describe the program that exists. Version control already describes programs that used to exist. Mixing those temporal roles forces every reader to classify code before understanding it: executable truth and historical residue occupy the same visual channel. The repository then stops being a precise statement of the current system.

### Trigger When

Trigger when obsolete functions, branches, declarations, imports, or implementation fragments remain in comments for possible future reuse or fear of losing history.

### Do Not Trigger When

- Short illustrative snippets in documentation comments are explanatory rather than preserving removed production code.
- A comment cites a protocol fragment or spec excerpt that is not former implementation being warehoused.
- Disabled code behind an explicit, tested feature flag is not a comment archive (though it may be other defects).
- TODO notes that describe missing work without embedding old functions are not commented-out code.

### Distinguish From

`dead-code-delivered` is still compilable but unreachable/unused code. `comment-theater` is commentary that narrates obvious behavior. This rule is former implementation preserved as comments. Tie-break: if deleting the comment removes old code rather than knowledge, this rule owns the case.

### Decision Procedure

Ask whether deleting the comment changes the current program or removes unique explanatory knowledge. If neither, version control is its proper home.

### Examples

- positive: a former handler left in a block comment “in case we need it,” next to the live replacement.
- near-miss: a doc comment showing a one-line protocol example that is not the old implementation.
- counterexample: delete the commented implementation; recover from version control if it is ever needed.

## Advice

### What To Do Now

Delete the commented implementation. Recover it from version control if it ever becomes relevant again. Version control is who owns retired implementation; the working tree is who owns only the present program and durable non-obvious knowledge.

### Why This Matters

A source file is valuable because readers can treat its visible structure as present tense. Commented code breaks that compact: it looks operational, carries stale names and assumptions, and competes for attention even though the compiler cannot verify it. The result is uncertainty without capability.

### Repair Strategy

Remove the dead fragment. If it contains a durable design reason not captured elsewhere, preserve the reason—not the old implementation—in the owning documentation or decision record.

### Decision Branches

- If the comment is former implementation kept “just in case,” delete it.
- If a durable why would be lost, record that reason in the owning decision or docs, still without the old code.
- If the snippet is genuinely explanatory and not a warehouse, leave the explanation, not the archive.

### Common Wrong Fixes

- Do not wrap the fragment in another comment or preprocessor branch.
- Do not move it to a “temporary” archive file in the working tree.
- Do not retain it because it may be useful someday; Git is built for that possibility.
- Do not convert it to an always-false runtime branch to “keep it compiling.”

### Verification

The source should contain only current implementation plus comments that convey non-obvious durable knowledge. The invariant is that visible source is present tense: no block asks the reader to classify it as historical residue.

### Done When

A reader never has to ask whether a visible block is old code being kept “just in case”; the file states only the program and the knowledge that govern it now.

### Nudge

Keep the working tree about the present. Delete commented-out implementation; history already has a lossless archive.
