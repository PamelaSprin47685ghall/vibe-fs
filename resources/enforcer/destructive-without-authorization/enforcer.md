# destructive-without-authorization — Enforcer

## Definition
A destructive action is unauthorized when it can irreversibly remove or overwrite state without both explicit authority for the action and verified identity of the target.

## Governing Principle
Irreversibility changes the proof burden. For additive or inspectable actions, a mistake can often be corrected later; deletion collapses possible futures. Therefore “I probably know what this is” is not sufficient evidence. Safe destruction requires two independent facts: someone entitled to decide has authorized the class of action, and the concrete object about to be destroyed is the intended object.

## Trigger When
Trigger before deleting or overwriting data, files, branches, worktrees, external resources, credentials, or other nontrivial state when authorization or target verification is implicit, inferred, or stale.

## Do Not Trigger When
- The target is an ephemeral, reproducible local artifact whose deletion is already part of an explicit scoped operation and cannot affect user or external state.
- The operation is a documented, authorized cleanup of a machine-checked temp/build directory the user or job just created.
- A dry-run or inspect-only command lists candidates without mutating them.
- The user or owning system just named the exact target in the same turn and the command echoes that identity before acting.

## Distinguish From
`scope-creep` concerns work beyond intent. `secret-in-code` concerns credential exposure. This rule is specifically the missing authority/identity proof before irreversible change. Tie-break: if the act is irreversible and either authority or target identity is inferred, this rule owns the case even when the broader task was in scope.

## Decision Procedure
Ask two separate questions: who authorized this destructive class of action, and what evidence proves this exact target is the authorized target? Both must have concrete answers before execution.

## Examples
- positive: deleting a branch or directory because the current path “looks like” the intended one, without an independent identity check.
- near-miss: removing a job-scoped temp directory the same process created, under an explicit cleanup contract.
- counterexample: require explicit authority and verify the concrete target immediately before the destructive step.

## Nudge
Destruction needs two proofs: authority and identity. Do not proceed on inference where a wrong target cannot be trivially restored.
