# recovery-by-filesystem-state — Lesson

## Opinion

### Definition

Recovery by filesystem state happens when restart logic infers **business/lifecycle progress** from incidental path residue rather than from a durable record designed to carry that fact.

The filesystem is not automatically the problem. A file can absolutely be the authoritative durable store. The defect is more specific: **presence, absence, filename, directory shape, temp residue, or worktree layout is being asked to mean more than the storage protocol actually commits.**

### Governing Principle

The common failure is **accidental commit points**. An implementation detail happens to be created near a semantic transition, then recovery promotes that coincidence into protocol. Later a refactor changes creation/cleanup order and silently changes recovery semantics without changing any domain code.

### Trigger When

Fire this rule when recovery says things like:

- "directory exists, therefore job was created/committed";
- "temp file is gone, therefore publish completed";
- "worktree branch exists, therefore integration succeeded";
- "`.done` file exists, therefore external effect happened";
- "lock file remains, therefore owner is still alive";
- "filename starts with `failed-`, so the workflow is failed";
- "there are N files, so phase N completed."

### Do Not Trigger When

Do not fire when the filesystem artifact is itself the explicitly designed store. A `state.json` written by atomic rename, with version/schema/checksum and documented commit semantics, can own lifecycle truth. A SQLite file is still "filesystem state" physically, but its transaction contract is not incidental directory residue. The distinction is whether the contents/commit protocol are authoritative by design.

Also do not fire when path existence is merely discovery: code locates a file, then parses its versioned durable record and bases decisions on that record.

### Distinguish From

Nearby rules:

- `log-as-recovery-protocol` — diagnostic prose is promoted into restart authority;
- `snapshot-as-truth` — derived projection outranks source facts;
- `leftover-scaffolding` — stale files remain, but recovery may not depend on them;
- `resource-not-scoped` — resource lifetime leaks and leaves residue.

### Decision Procedure

Use this rule when the sharp claim is: **incidental artifact topology is deciding what the workflow believes happened.**

The decisive crash exercise is to stop the program just before and just after every artifact creation/removal. If the same path shape can correspond to two different semantic realities, that shape cannot safely be the recovery fact.

A robust recovery protocol names milestones directly: `JobAccepted`, `PublishCommitted`, `IntegrationCompleted`, `OwnerLeaseExpiresAt`, versioned state row, journal event, transaction status. Artifacts may be referenced by those facts, but should not substitute for them.

### Examples

A temp directory may exist before validation. A lock file may survive a crash. A worktree may remain after a failed integration. A renamed file may indicate either success or interrupted cleanup depending on where the process died. Those artifacts record pieces of execution topology, not necessarily semantic milestones.

## Advice

### What To Do Now

Move recovery truth into an explicit durable protocol.

For every restart decision currently based on path shape, name the semantic fact the code is trying to infer. Then persist that fact at the real commit point using a store whose atomicity/durability semantics are designed for recovery.

### Why This Matters

Bad inference:

```text
worktree exists → job must have started
.done file exists → publish must have committed
temp file absent → cleanup must have finished
```

Better protocol:

```text
JobAccepted(jobId, ...)
PublishCommitted(publicationId, ...)
CleanupCompleted(resourceId, ...)
```

or an equivalent versioned state record/transaction.

Artifacts can still exist. The durable fact may name the worktree, blob, temp directory, branch, or file generation. But on restart the question "what happened?" is answered by the record; the artifact answers narrower questions such as "where are the bytes?" or "does cleanup remain?"

### Repair Strategy

If a file itself is intended to be the store, strengthen it until that is honest:

- versioned schema;
- explicit commit protocol such as write-temp + fsync + atomic rename where the platform contract supports it;
- checksum/digest when corruption/substitution matters;
- generation/owner identity;
- documented behavior for absent, old-version, and corrupt state;
- no sibling filename conventions carrying hidden lifecycle semantics.

### Decision Branches

Recovery must follow the explicit durable facts and either ignore, validate, reuse, or clean the artifacts according to those facts. Renaming implementation directories should not change lifecycle meaning.

Also test the inverse: corrupt or remove the actual durable lifecycle record. Recovery should fail/reconcile according to the store contract rather than silently reconstructing truth from residue and thereby masking loss of the real authority.

### Common Wrong Fixes

Common fake repairs:

- add more filename prefixes (`pending-`, `done-`, `failed-`);
- compare mtimes to guess which phase happened last;
- create sentinel files without atomic commit semantics and call them "durable events";
- write a journal too, but keep recovery reading the filesystem heuristics because migration is inconvenient;
- clean stale artifacts more aggressively instead of removing their semantic authority;
- persist lifecycle status in a path name so rename order becomes the state machine;
- assume a PID/lock file proves the process named inside is still the rightful owner.

### Verification

Verification should deliberately create misleading residue:

- artifact created, semantic commit not reached;
- semantic commit reached, cleanup artifact still present;
- old artifact from previous generation/session;
- partially initialized directory;
- renamed/reorganized implementation paths;
- stale lock/PID file after crash;
- missing cache artifact despite committed lifecycle fact.

### Done When

You are done when every restart decision can cite a typed/versioned durable fact, and filesystem topology is demoted to data/resource evidence unless the file itself is the deliberately designed store.

> Recovery should read commitments, not archaeology.

### Nudge

> A path can prove that a path exists. It cannot prove a business transition unless the protocol explicitly made that existence the commit.
