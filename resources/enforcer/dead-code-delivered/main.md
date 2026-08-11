# dead-code-delivered — Main

## What To Do Now
Delete production code that has no current caller, activation path, or contractual role. Version control is who owns history; the working tree is who owns only paths with a present role.

## Why This Matters
Dead code has zero runtime value but nonzero reasoning cost. It creates false alternatives in the reader’s model and makes searches, refactors, security review, and ownership analysis less trustworthy. Keeping it “for later” transfers uncertainty to every future maintainer.

## Repair Strategy
Trace references and activation paths, remove the dead surface, then clean imports, tests, flags, and documentation that existed only to support it. Preserve historical availability in version control rather than the source tree.

## Decision Branches
- If no caller, activation path, or contract remains, delete the production path and its exclusive support files.
- If an owner still claims the path as a tested extension or compatibility surface, keep it only with that explicit contract.
- If the path is commented out rather than executable, this is not this rule; restore or delete under the comment rule.

## Common Wrong Fixes
- Do not comment the dead path out; comments are not an archive.
- Do not hide it behind an always-false flag or unreachable branch.
- Do not move it to a “legacy” folder that still ships.
- Do not keep it “for later” because version control already stores history.

## Verification
Build and test after removal. Search for references to the retired names and ensure no documented supported behavior depended on them. The invariant is that every remaining production path has a present reason to exist.

## Done When
Every production path has a current reason to exist, and the source tree no longer asks readers to distinguish the live system from its abandoned possibilities.
