# tool-error-ignored — Main

## What To Do Now
Stop. Read the error. Fix the cause or document why it is irrelevant with compensating verification. Do not continue as if it succeeded.

## Repair Strategy
Re-run the failing tool. Address root cause. If the tool is wrong, fix the invocation. If the check is obsolete, remove or update it—do not ignore output.

## Decision Branches
If multiple errors cascade, fix the first root error and re-run. If flaky infrastructure, bound retries then fail—do not ignore.

## Wrong Fixes
Scrolling past red output. `|| true` on critical checks. Claiming success because a later command looked fine.

## Verification
The previously failing command is green, or a written exception with alternate proof exists.

## Done When
No unresolved tool errors remain on the critical path without explicit accounting.

## Scope and Authority
Agent and developer workflows consuming tool output.
