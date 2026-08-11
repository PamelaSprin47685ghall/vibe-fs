# recovery-by-filesystem-state — Enforcer

## Definition
Recovery by filesystem state infers workflow progress from incidental files, directories, temp names, or working-tree shape instead of from durable lifecycle facts explicitly written for recovery.

## Governing Principle
Filesystem residue records implementation side effects, not necessarily business commitments. A temp file may survive a crash before completion; a directory may exist because creation preceded validation; cleanup may lag success. Inferring lifecycle from shape promotes accidents of execution order into protocol states and makes refactoring filenames/layout equivalent to changing recovery semantics.

## Trigger When
Trigger when restart logic decides what work happened by checking existence, path names, temp artifacts, branch/worktree shape, or directory contents that were not designed as versioned durable facts.

## Do Not Trigger When
Do not trigger when the filesystem artifact is itself the explicit durable store and recovery reads schema-backed contents with defined commit semantics rather than incidental presence.

## Distinguish From
log-as-recovery-protocol elevates diagnostics. snapshot-as-truth elevates a projection. This rule elevates incidental filesystem topology into lifecycle authority.

## Decision Procedure
For each recovery decision, ask which durable fact proves it. If the answer is “because this file/directory happens to exist,” record the lifecycle fact explicitly instead.

## Nudge
Filesystem residue is evidence of execution, not proof of business progress. Recover from explicit durable lifecycle facts and treat incidental paths as disposable implementation detail.
