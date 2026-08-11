# commented-out-code — Main

## What To Do Now
Delete the commented implementation. Recover it from version control if it ever becomes relevant again. Version control is who owns retired implementation; the working tree is who owns only the present program and durable non-obvious knowledge.

## Why This Matters
A source file is valuable because readers can treat its visible structure as present tense. Commented code breaks that compact: it looks operational, carries stale names and assumptions, and competes for attention even though the compiler cannot verify it. The result is uncertainty without capability.

## Repair Strategy
Remove the dead fragment. If it contains a durable design reason not captured elsewhere, preserve the reason—not the old implementation—in the owning documentation or decision record.

## Decision Branches
- If the comment is former implementation kept “just in case,” delete it.
- If a durable why would be lost, record that reason in the owning decision or docs, still without the old code.
- If the snippet is genuinely explanatory and not a warehouse, leave the explanation, not the archive.

## Common Wrong Fixes
- Do not wrap the fragment in another comment or preprocessor branch.
- Do not move it to a “temporary” archive file in the working tree.
- Do not retain it because it may be useful someday; Git is built for that possibility.
- Do not convert it to an always-false runtime branch to “keep it compiling.”

## Verification
The source should contain only current implementation plus comments that convey non-obvious durable knowledge. The invariant is that visible source is present tense: no block asks the reader to classify it as historical residue.

## Done When
A reader never has to ask whether a visible block is old code being kept “just in case”; the file states only the program and the knowledge that govern it now.
