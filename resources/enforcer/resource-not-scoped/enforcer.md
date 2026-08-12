# resource-not-scoped — Enforcer

A resource is not scoped when acquiring it creates an obligation whose end is merely a convention in later control flow.

Open file, start process, borrow connection, subscribe stream, create temp worktree, allocate session, acquire terminal, mount handle, hold lease — each act creates more than a value. It creates a **temporal obligation**: somebody now owns this thing, and somebody must end that ownership exactly once.

The defect appears when acquire and release are written as unrelated operations:

```text
let r = acquire()
...
if something then return   // who closes r?
...
release(r)
```

Every new branch, exception, cancellation path, retry, callback, and early return now has to remember the same lifetime rule. That is path enumeration masquerading as ownership.

A scoped lifetime turns the obligation into structure. The code shape itself says: this owner acquires here; every exit from this scope releases; transfer, if any, must be explicit.

Fire this rule when:

- cleanup is performed manually at several exits;
- a handle can escape without the type/API showing who becomes responsible for disposal;
- a process/session/worktree is created in one module and “eventually cleaned up” elsewhere;
- exception/cancellation paths rely on best-effort cleanup code that is not mechanically tied to acquisition;
- a subscription/event listener has no visible unsubscribe lifetime;
- a temp file/directory survives because one failure path returned before cleanup;
- tests regularly need global teardown sweeps because local owners cannot be trusted to release what they acquired.

Do not fire just because lifetime is long. Process-wide resources may legitimately be owned by process shutdown. Pools legitimately own long-lived connections while callers own short leases. Background workflows may own a session beyond the initiating request. The question is whether **ownership duration is explicit and structurally enforced**, not whether the resource is short-lived.

Likewise, finalizers/GC can be a defensive backstop but rarely qualify as the primary semantic owner for scarce or externally visible resources. “Eventually collected” is not a lifecycle contract for a file lock, process, socket, permit, worktree, or subscription.

Nearby rules:

- `cancellation-not-propagated` — owner cancels but child work survives;
- `permit-leak` — the resource is specifically finite concurrency capacity;
- `leftover-scaffolding` / `spike-not-cleaned` — artifacts remain after temporary engineering work, not necessarily because runtime lifetime was unscoped.

A good diagnostic is to point at every acquisition and ask one sentence:

> From syntax/API alone, can I tell who owns this resource now, and what event ends that ownership?

If the answer requires mentally tracing all returns, remembering a comment, or knowing that some distant shutdown sweep will “probably” find it, lifetime is not scoped.

> Resource correctness includes **when the resource stops existing**. If release is a memory test for callers, ownership has not been encoded strongly enough.
