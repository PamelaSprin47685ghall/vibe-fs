# dead-code-delivered — Main

## What To Do Now
Delete production code that has no current caller, activation path, or contractual role.

## Why This Matters
Dead code has zero runtime value but nonzero reasoning cost. It creates false alternatives in the reader’s model and makes searches, refactors, security review, and ownership analysis less trustworthy. Keeping it “for later” transfers uncertainty to every future maintainer.

## Repair Strategy
Trace references and activation paths, remove the dead surface, then clean imports, tests, flags, and documentation that existed only to support it. Preserve historical availability in version control rather than the source tree.

## Wrong Fixes
Do not comment it out, hide it behind an always-false flag, or move it to a “legacy” folder. Those preserve the same ambiguity under a different label.

## Verification
Build and test after removal. Search for references to the retired names and ensure no documented supported behavior depended on them.

## Done When
Every production path has a current reason to exist, and the source tree no longer asks readers to distinguish the live system from its abandoned possibilities.
