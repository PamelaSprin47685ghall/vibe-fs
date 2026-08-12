# resource-not-scoped — Main

Make lifetime structural.

Put acquisition and release under one owning construct: `using`, `defer`, bracket, `try/finally`, scoped lease, disposable object, task group, context manager, or another mechanism whose semantics guarantee release across **all exits**, not only the exits somebody remembered when writing the function.

The core invariant is:

> **Every acquisition creates exactly one ownership obligation, and structure either discharges it or explicitly transfers it.**

Keep the resource inside the smallest scope that truly owns it. A handle that never escapes is easier to reason about than a handle passed through five layers with an oral tradition about who closes it.

When ownership must move, make transfer visible. Good transfer semantics usually have one of these shapes:

- move/linear ownership: sender cannot use/release after transfer;
- lease: receiver owns temporary use, pool/parent retains underlying resource ownership;
- ref-counted/shared lifetime: semantics explicitly state when the last owner releases;
- durable workflow ownership: resource/session/job belongs to a named longer-lived principal.

Do not simulate transfer by “returning the handle and documenting that callers should close it.” If that is the contract, encode it in the type/API and test it.

Common fake repairs:

- add `close()` to every currently known `return` branch;
- install a global shutdown sweep to mop up leaked local resources;
- rely on GC/finalizers for sockets, processes, file locks, worktrees, subscriptions, or scarce handles;
- swallow disposal errors so tests look clean while teardown actually failed;
- put cleanup in a callback that itself may never run after cancellation;
- keep a global registry of live resources solely because no local scope owns them coherently;
- make a singleton own everything, then lose the ability to tell which request/workflow caused a resource to exist.

Pay attention to nested lifetimes. If A creates B and B creates C, teardown usually belongs in reverse ownership order: C, then B, then A. Structured scopes make this visible; scattered cleanup often gets the order wrong and creates shutdown races.

Verification should force every exit class:

- normal success;
- early return;
- exception;
- cancellation;
- timeout;
- partial initialization where acquisition N succeeds and acquisition N+1 fails;
- ownership transfer;
- repeated cleanup / idempotent disposal if the API permits it.

Instrument real resource consequences when possible: file descriptors, processes, subscriptions, worktrees, temporary directories, sessions, permits. “Dispose was called” is weaker than “the externally visible resource is actually gone,” especially for asynchronous teardown.

You are done when a reviewer can infer the complete lifetime from local structure and explicit transfers. They should not need a control-flow proof over every branch or a repository-wide search for the matching `close()`.

> Acquire and release are not two unrelated calls. They are the opening and closing edge of one ownership fact.
