# resource-not-scoped — Lesson

## Opinion

### Definition

A resource is not scoped when acquiring it creates an obligation whose end is merely a convention in later control flow.

Open file, start process, borrow connection, subscribe stream, create temp worktree, allocate session, acquire terminal, mount handle, hold lease — each act creates more than a value. It creates a **temporal obligation**: somebody now owns this thing, and somebody must end that ownership exactly once.

### Governing Principle

The question is whether **ownership duration is explicit and structurally enforced**, not whether the resource is short-lived.

Likewise, finalizers/GC can be a defensive backstop but rarely qualify as the primary semantic owner for scarce or externally visible resources. "Eventually collected" is not a lifecycle contract for a file lock, process, socket, permit, worktree, or subscription.

### Trigger When

Fire this rule when:

- cleanup is performed manually at several exits;
- a handle can escape without the type/API showing who becomes responsible for disposal;
- a process/session/worktree is created in one module and "eventually cleaned up" elsewhere;
- exception/cancellation paths rely on best-effort cleanup code that is not mechanically tied to acquisition;
- a subscription/event listener has no visible unsubscribe lifetime;
- a temp file/directory survives because one failure path returned before cleanup;
- tests regularly need global teardown sweeps because local owners cannot be trusted to release what they acquired.

### Do Not Trigger When

Do not fire just because lifetime is long. Process-wide resources may legitimately be owned by process shutdown. Pools legitimately own long-lived connections while callers own short leases. Background workflows may own a session beyond the initiating request.

### Distinguish From

Nearby rules:

- `cancellation-not-propagated` — owner cancels but child work survives;
- `permit-leak` — the resource is specifically finite concurrency capacity;
- `leftover-scaffolding` / `spike-not-cleaned` — artifacts remain after temporary engineering work, not necessarily because runtime lifetime was unscoped.

### Decision Procedure

A good diagnostic is to point at every acquisition and ask one sentence:

> From syntax/API alone, can I tell who owns this resource now, and what event ends that ownership?

If the answer requires mentally tracing all returns, remembering a comment, or knowing that some distant shutdown sweep will "probably" find it, lifetime is not scoped.

### Examples

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

## Advice

### What To Do Now

Make lifetime structural.

Put acquisition and release under one owning construct: `using`, `defer`, bracket, `try/finally`, scoped lease, disposable object, task group, context manager, or another mechanism whose semantics guarantee release across **all exits**, not only the exits somebody remembered when writing the function.

The core invariant is:

> **Every acquisition creates exactly one ownership obligation, and structure either discharges it or explicitly transfers it.**

Keep the resource inside the smallest scope that truly owns it. A handle that never escapes is easier to reason about than a handle passed through five layers with an oral tradition about who closes it.

### Why This Matters

When ownership must move, make transfer visible. Good transfer semantics usually have one of these shapes:

- move/linear ownership: sender cannot use/release after transfer;
- lease: receiver owns temporary use, pool/parent retains underlying resource ownership;
- ref-counted/shared lifetime: semantics explicitly state when the last owner releases;
- durable workflow ownership: resource/session/job belongs to a named longer-lived principal.

Do not simulate transfer by "returning the handle and documenting that callers should close it." If that is the contract, encode it in the type/API and test it.

### Repair Strategy

Pay attention to nested lifetimes. If A creates B and B creates C, teardown usually belongs in reverse ownership order: C, then B, then A. Structured scopes make this visible; scattered cleanup often gets the order wrong and creates shutdown races.

### Decision Branches

Verification should force every exit class:

- normal success;
- early return;
- exception;
- cancellation;
- timeout;
- partial initialization where acquisition N succeeds and acquisition N+1 fails;
- ownership transfer;
- repeated cleanup / idempotent disposal if the API permits it.

### Common Wrong Fixes

Common fake repairs:

- add `close()` to every currently known `return` branch;
- install a global shutdown sweep to mop up leaked local resources;
- rely on GC/finalizers for sockets, processes, file locks, worktrees, subscriptions, or scarce handles;
- swallow disposal errors so tests look clean while teardown actually failed;
- put cleanup in a callback that itself may never run after cancellation;
- keep a global registry of live resources solely because no local scope owns them coherently;
- make a singleton own everything, then lose the ability to tell which request/workflow caused a resource to exist.

### Verification

Instrument real resource consequences when possible: file descriptors, processes, subscriptions, worktrees, temporary directories, sessions, permits. "Dispose was called" is weaker than "the externally visible resource is actually gone," especially for asynchronous teardown.

### Done When

You are done when a reviewer can infer the complete lifetime from local structure and explicit transfers. They should not need a control-flow proof over every branch or a repository-wide search for the matching `close()`.

> Acquire and release are not two unrelated calls. They are the opening and closing edge of one ownership fact.

### Nudge

> Resource correctness includes **when the resource stops existing**. If release is a memory test for callers, ownership has not been encoded strongly enough.
