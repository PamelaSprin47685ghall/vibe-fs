# recovery-by-filesystem-state — Main

Move recovery truth into an explicit durable protocol.

For every restart decision currently based on path shape, name the semantic fact the code is trying to infer. Then persist that fact at the real commit point using a store whose atomicity/durability semantics are designed for recovery.

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

Artifacts can still exist. The durable fact may name the worktree, blob, temp directory, branch, or file generation. But on restart the question “what happened?” is answered by the record; the artifact answers narrower questions such as “where are the bytes?” or “does cleanup remain?”

If a file itself is intended to be the store, strengthen it until that is honest:

- versioned schema;
- explicit commit protocol such as write-temp + fsync + atomic rename where the platform contract supports it;
- checksum/digest when corruption/substitution matters;
- generation/owner identity;
- documented behavior for absent, old-version, and corrupt state;
- no sibling filename conventions carrying hidden lifecycle semantics.

Common fake repairs:

- add more filename prefixes (`pending-`, `done-`, `failed-`);
- compare mtimes to guess which phase happened last;
- create sentinel files without atomic commit semantics and call them “durable events”;
- write a journal too, but keep recovery reading the filesystem heuristics because migration is inconvenient;
- clean stale artifacts more aggressively instead of removing their semantic authority;
- persist lifecycle status in a path name so rename order becomes the state machine;
- assume a PID/lock file proves the process named inside is still the rightful owner.

Verification should deliberately create misleading residue:

- artifact created, semantic commit not reached;
- semantic commit reached, cleanup artifact still present;
- old artifact from previous generation/session;
- partially initialized directory;
- renamed/reorganized implementation paths;
- stale lock/PID file after crash;
- missing cache artifact despite committed lifecycle fact.

Recovery must follow the explicit durable facts and either ignore, validate, reuse, or clean the artifacts according to those facts. Renaming implementation directories should not change lifecycle meaning.

Also test the inverse: corrupt or remove the actual durable lifecycle record. Recovery should fail/reconcile according to the store contract rather than silently reconstructing truth from residue and thereby masking loss of the real authority.

You are done when every restart decision can cite a typed/versioned durable fact, and filesystem topology is demoted to data/resource evidence unless the file itself is the deliberately designed store.

> Recovery should read commitments, not archaeology.