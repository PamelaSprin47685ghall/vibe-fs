# recovery-by-filesystem-state — Enforcer

## Definition
Recovery by filesystem state infers workflow progress from incidental files, directories, temp names, or working-tree shape instead of from durable lifecycle facts explicitly written for recovery. The root-cause is that incidental path presence is treated as lifecycle authority, so accidents of execution order become the recovery protocol.

## Governing Principle
Filesystem residue records implementation side effects, not necessarily business commitments. A temp file may survive a crash before completion; a directory may exist because creation preceded validation; cleanup may lag success. Inferring lifecycle from shape promotes accidents of execution order into protocol states and makes refactoring filenames/layout equivalent to changing recovery semantics.

## Trigger When
Trigger when restart logic decides what work happened by checking existence, path names, temp artifacts, branch/worktree shape, or directory contents that were not designed as versioned durable facts.

## Do Not Trigger When
- The filesystem artifact is itself the explicit durable store and recovery reads schema-backed contents with defined commit semantics rather than incidental presence.
- Existence checks are only used to locate a file whose contents are then parsed as the versioned record.
- The path is a cache that is discarded and rebuilt from an independent durable log on mismatch.
- A test fixture inspects files as an observation of a documented store, not as the recovery protocol.

## Distinguish From
log-as-recovery-protocol elevates diagnostics. snapshot-as-truth elevates a projection. This rule elevates incidental filesystem topology into lifecycle authority. Tie-break: fire here when presence/shape of paths decides progress; fire log-as-recovery-protocol when log lines become the protocol; fire snapshot-as-truth when a derived checkpoint outranks source facts.

## Decision Procedure
For each recovery decision, ask which durable fact proves it. If the answer is “because this file/directory happens to exist,” record the lifecycle fact explicitly instead.

## Examples
- positive: restart treats `tmp/job.lock` existing as “job committed” even though the lock is created before validation.
- near-miss: recovery opens `state.json` and reads a versioned schema with an atomic rename commit; the file is the store, not residue.
- counterexample: a build cache directory is deleted and rebuilt from the event log whenever its digest disagrees.

## Nudge
Filesystem residue is evidence of execution, not proof of business progress. Recover from explicit durable lifecycle facts and treat incidental paths as disposable implementation detail.
