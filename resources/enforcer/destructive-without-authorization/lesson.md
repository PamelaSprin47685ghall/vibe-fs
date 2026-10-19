# destructive-without-authorization — Lesson

## Opinion

### Definition

A destructive action is unauthorized when it can irreversibly remove or overwrite state without both explicit authority for the action and verified identity of the target. The root-cause is that irreversibility is treated as an ordinary step whose authority and exact target can be inferred.

### Governing Principle

Irreversibility changes the proof burden. For additive or inspectable actions, a mistake can often be corrected later; deletion collapses possible futures. Therefore “I probably know what this is” is not sufficient evidence. Safe destruction requires two independent facts: someone entitled to decide has authorized the class of action, and the concrete object about to be destroyed is the intended object.

### Trigger When

Trigger before deleting or overwriting data, files, branches, worktrees, external resources, credentials, or other nontrivial state when authorization or target verification is implicit, inferred, or stale.

### Do Not Trigger When

- The target is an ephemeral, reproducible local artifact whose deletion is already part of an explicit scoped operation and cannot affect user or external state.
- The operation is a documented, authorized cleanup of a machine-checked temp/build directory the user or job just created.
- A dry-run or inspect-only command lists candidates without mutating them.
- The user or owning system just named the exact target in the same turn and the command echoes that identity before acting.

### Distinguish From

`scope-creep` concerns work beyond intent. `secret-in-code` concerns credential exposure. This rule is specifically the missing authority/identity proof before irreversible change. Tie-break: if the act is irreversible and either authority or target identity is inferred, this rule owns the case even when the broader task was in scope.

### Decision Procedure

Ask two separate questions: who authorized this destructive class of action, and what evidence proves this exact target is the authorized target? Both must have concrete answers before execution.

### Examples

- positive: deleting a branch or directory because the current path “looks like” the intended one, without an independent identity check.
- near-miss: removing a job-scoped temp directory the same process created, under an explicit cleanup contract.
- counterexample: require explicit authority and verify the concrete target immediately before the destructive step.

## Advice

### What To Do Now

Establish explicit authorization for the destructive operation and independently verify the concrete target immediately before executing it. The authorizing principal is who owns the class of destruction; the live identity check is who owns proof of the exact target.

### Why This Matters

Deletion removes evidence and options. The expected cost of a mistaken destructive action is therefore dominated by rare catastrophic cases, not by average convenience. Safety comes from refusing to compress “may delete” and “this is the thing to delete” into one assumption.

### Repair Strategy

Resolve the target through authoritative identity, surface the destructive effect clearly, and require the appropriate authorization boundary. For automation, make the target and scope machine-checkable rather than relying on path similarity or operator memory.

### Decision Branches

- If authority for this class of destruction is missing, stop and obtain it before any irreversible step.
- If authority exists but the target identity is inferred, stale, or similar-looking, re-resolve the exact object and confirm it.
- If the artifact is ephemeral, scoped, and reproducible by contract, proceed under that scoped cleanup; this rule does not block it.

### Common Wrong Fixes

- Do not treat broad project access as permission for every destructive act.
- Do not rely on a guessed path, current directory, branch name, or stale listing to establish identity.
- Do not skip confirmation because the name is “probably unique.”
- Do not batch-delete siblings to “save a round trip” after identifying one target.

### Verification

Before execution, the displayed/checked identity must match the authorized target and scope. Afterward, verify that no sibling resource was affected. The invariant is that irreversible change is traceable to explicit authority over a precisely identified target.

### Done When

Every irreversible change is traceable to explicit authority over a precisely identified target, with no destructive step depending on guesswork.

### Nudge

Destruction needs two proofs: authority and identity. Do not proceed on inference where a wrong target cannot be trivially restored.
