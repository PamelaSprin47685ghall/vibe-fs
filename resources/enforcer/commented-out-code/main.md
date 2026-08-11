# commented-out-code — Main

## What To Do Now
Delete the commented implementation. Recover it from version control if it ever becomes relevant again.

## Why This Matters
A source file is valuable because readers can treat its visible structure as present tense. Commented code breaks that compact: it looks operational, carries stale names and assumptions, and competes for attention even though the compiler cannot verify it. The result is uncertainty without capability.

## Repair Strategy
Remove the dead fragment. If it contains a durable design reason not captured elsewhere, preserve the reason—not the old implementation—in the owning documentation or decision record.

## Wrong Fixes
Do not wrap the fragment in another comment, preprocessor branch, or “temporary” archive file. Do not retain it because it may be useful someday; Git is specifically built for that possibility.

## Verification
The source should contain only current implementation plus comments that convey non-obvious durable knowledge.

## Done When
A reader never has to ask whether a visible block is old code being kept “just in case”; the file states only the program and the knowledge that govern it now.
