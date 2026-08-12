# cancellation-not-propagated — Main

Repair cancellation as an ownership protocol, not as exception plumbing.

Start from the principal that can cancel: request, session, workflow, user command, parent task, or lifecycle scope. Thread one cancellation capability through every effect that remains owned by that principal. Where APIs use different mechanisms — abort controller, cancellation token, process signal, socket close, task group, session abort — adapt them at the boundary instead of silently dropping the signal.

The desired property is:

> **Once an owner withdraws authority, no effect it still owns may continue past the cancellation boundary.**

That does not require every operation to stop at the same instruction. Some libraries only cancel at defined safe points. Some external services cannot recall a request already committed. The important part is that those semantics are explicit: after cancellation, either the effect is known not to happen, known to have happened, or its outcome is explicitly unknown and recovery handles that fact. “We returned Cancelled” is not enough.

If work truly needs to outlive the parent, transfer ownership before detach. A proper transfer usually needs:

- a new durable owner identity;
- a durable work record / queue item / job id;
- independent cancellation and retry policy;
- a destination for completion/failure;
- no remaining dependency on the vanished parent's in-memory scope.

Common fake repairs:

- `catch (Cancelled) { return }` while inner calls keep running;
- dropping a promise/future and assuming the underlying operation stopped;
- aborting the HTTP response but not the database/process/tool work triggered by it;
- cancelling only direct children while grandchildren continue;
- setting an `isCancelled` flag that inner effects never observe;
- ignoring late results while those results are still allowed to mutate shared or durable state;
- calling detached work “background” without naming a new owner;
- sending a process signal but never awaiting/confirming teardown, leaving resource lifetime ambiguous.

Verification should cancel at **every meaningful phase**, not only immediately after start:

- before a child begins;
- while a network call is pending;
- while a process is running;
- after a child completed but before its result commits;
- during cleanup;
- after supersession by newer work.

Observe physical consequences: processes exit, sockets close, permits return, child sessions stop, no later callback mutates state, and no stale result is published after cancellation won.

For non-recallable external effects, test the uncertain-outcome branch explicitly. Cancellation cannot undo a charge already accepted by a remote service; it must not lie that “nothing happened.” Pair this rule with idempotency/reconciliation when effect acknowledgement can be lost.

You are done when logical lifetime and physical lifetime agree, except at boundaries where ownership has been deliberately transferred or the protocol explicitly records an unknown/irreversible effect.

> “I stopped waiting” is not a cancellation guarantee. The question is what the owned work itself is still allowed to do.
