# recovery-by-filesystem-state — Enforcer

Recovery by filesystem state happens when restart logic infers **business/lifecycle progress** from incidental path residue rather than from a durable record designed to carry that fact.

The filesystem is not automatically the problem. A file can absolutely be the authoritative durable store. The defect is more specific: **presence, absence, filename, directory shape, temp residue, or worktree layout is being asked to mean more than the storage protocol actually commits.**

A temp directory may exist before validation. A lock file may survive a crash. A worktree may remain after a failed integration. A renamed file may indicate either success or interrupted cleanup depending on where the process died. Those artifacts record pieces of execution topology, not necessarily semantic milestones.

Fire this rule when recovery says things like:

- “directory exists, therefore job was created/committed”;
- “temp file is gone, therefore publish completed”;
- “worktree branch exists, therefore integration succeeded”;
- “`.done` file exists, therefore external effect happened”;
- “lock file remains, therefore owner is still alive”;
- “filename starts with `failed-`, so the workflow is failed”;
- “there are N files, so phase N completed.”

The common failure is **accidental commit points**. An implementation detail happens to be created near a semantic transition, then recovery promotes that coincidence into protocol. Later a refactor changes creation/cleanup order and silently changes recovery semantics without changing any domain code.

Do not fire when the filesystem artifact is itself the explicitly designed store. A `state.json` written by atomic rename, with version/schema/checksum and documented commit semantics, can own lifecycle truth. A SQLite file is still “filesystem state” physically, but its transaction contract is not incidental directory residue. The distinction is whether the contents/commit protocol are authoritative by design.

Also do not fire when path existence is merely discovery: code locates a file, then parses its versioned durable record and bases decisions on that record.

Nearby rules:

- `log-as-recovery-protocol` — diagnostic prose is promoted into restart authority;
- `snapshot-as-truth` — derived projection outranks source facts;
- `leftover-scaffolding` — stale files remain, but recovery may not depend on them;
- `resource-not-scoped` — resource lifetime leaks and leaves residue.

Use this rule when the sharp claim is: **incidental artifact topology is deciding what the workflow believes happened.**

The decisive crash exercise is to stop the program just before and just after every artifact creation/removal. If the same path shape can correspond to two different semantic realities, that shape cannot safely be the recovery fact.

A robust recovery protocol names milestones directly: `JobAccepted`, `PublishCommitted`, `IntegrationCompleted`, `OwnerLeaseExpiresAt`, versioned state row, journal event, transaction status. Artifacts may be referenced by those facts, but should not substitute for them.

> A path can prove that a path exists. It cannot prove a business transition unless the protocol explicitly made that existence the commit.