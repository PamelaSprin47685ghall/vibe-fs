# commented-out-code — Enforcer

## Definition
Commented-out implementation is dead code kept inside the living source as an informal archive.

## Governing Principle
Source should describe the program that exists. Version control already describes programs that used to exist. Mixing those temporal roles forces every reader to classify code before understanding it: executable truth and historical residue occupy the same visual channel. The repository then stops being a precise statement of the current system.

## Trigger When
Trigger when obsolete functions, branches, declarations, imports, or implementation fragments remain in comments for possible future reuse or fear of losing history.

## Do Not Trigger When
Do not trigger for short illustrative snippets in documentation comments whose purpose is explanatory rather than preserving removed production code.

## Distinguish From
dead-code-delivered is still compilable but unreachable/unused code. comment-theater is commentary that narrates obvious behavior. This rule is former implementation preserved as comments.

## Decision Procedure
Ask whether deleting the comment changes the current program or removes unique explanatory knowledge. If neither, version control is its proper home.

## Nudge
Keep the working tree about the present. Delete commented-out implementation; history already has a lossless archive.
