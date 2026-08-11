# commented-out-code — Enforcer

## Definition
Commented-out implementation is dead code kept inside the living source as an informal archive.

## Governing Principle
Source should describe the program that exists. Version control already describes programs that used to exist. Mixing those temporal roles forces every reader to classify code before understanding it: executable truth and historical residue occupy the same visual channel. The repository then stops being a precise statement of the current system.

## Trigger When
Trigger when obsolete functions, branches, declarations, imports, or implementation fragments remain in comments for possible future reuse or fear of losing history.

## Do Not Trigger When
- Short illustrative snippets in documentation comments are explanatory rather than preserving removed production code.
- A comment cites a protocol fragment or spec excerpt that is not former implementation being warehoused.
- Disabled code behind an explicit, tested feature flag is not a comment archive (though it may be other defects).
- TODO notes that describe missing work without embedding old functions are not commented-out code.

## Distinguish From
`dead-code-delivered` is still compilable but unreachable/unused code. `comment-theater` is commentary that narrates obvious behavior. This rule is former implementation preserved as comments. Tie-break: if deleting the comment removes old code rather than knowledge, this rule owns the case.

## Decision Procedure
Ask whether deleting the comment changes the current program or removes unique explanatory knowledge. If neither, version control is its proper home.

## Examples
- positive: a former handler left in a block comment “in case we need it,” next to the live replacement.
- near-miss: a doc comment showing a one-line protocol example that is not the old implementation.
- counterexample: delete the commented implementation; recover from version control if it is ever needed.

## Nudge
Keep the working tree about the present. Delete commented-out implementation; history already has a lossless archive.
