# cancellation-not-propagated — Lesson

## Opinion

### Definition

Cancellation is broken when the outer operation says “this work is over” while work that still belongs to that operation keeps running.

A request timeout, user abort, superseding command, session shutdown, or cancelled task means some principal has withdrawn authority for the work it owns. If child processes, network calls, agent runs, streams, database operations, timers, or callbacks continue after that point without an explicit ownership transfer, the runtime lifetime has escaped the logical lifetime.

### Governing Principle

That is not merely a resource leak. It is a lie about ownership.

The result is orphan work: effects with no remaining principal that is entitled to want them.

### Trigger When

Fire this rule when cancellation/abort is observed at one layer but an owned child effect has no causal path to that signal.

### Do Not Trigger When

Do not fire for truly detached work whose ownership was transferred **before** the parent returned. A durable outbox job, queue item, scheduler task, or independently owned workflow may legitimately outlive the initiating request. But detachment is not “we stopped awaiting it.” Real transfer answers: who owns it now, where is that ownership durable, who can cancel it, and who receives its completion.

Also do not confuse “result ignored” with “work cancelled.” Dropping the future/promise only stops *you* from listening. It says nothing about whether the external work stopped.

### Distinguish From

Nearby rules:

- `resource-not-scoped` — acquire/release lifetime is structurally unsafe;
- `permit-leak` — bounded concurrency capacity is never returned;
- `race-first-wins-semantics` — stale/losing work may continue, but the central defect is timing choosing truth;
- `partial-write-assumption` — cancellation may interrupt a write, but that rule concerns assuming no partial effect occurred.

### Decision Procedure

The key diagnostic is an ownership tree. Start at the cancelled operation and enumerate every child effect it caused. For each one, ask:

1. does the child still belong to this parent at cancellation time?
2. if yes, how does the cancellation signal reach it?
3. what proves its physical effect stopped or reached a defined cancellation boundary?
4. what cleanup is guaranteed?
5. if no, where exactly was ownership transferred?

If any answer is “we just stop awaiting it,” the work is not cancelled. It is abandoned while still armed.

### Examples

This is where some of the nastiest “impossible” incidents come from:

- the UI says Cancelled, then a stale request writes state thirty seconds later;
- a timed-out tool process keeps holding a file or port;
- a superseded background computation publishes its result after a newer computation already won;
- a client disconnect stops response handling but downstream billing/API work continues;
- a parent agent is aborted while child sessions keep consuming tokens and later report into a world that no longer expects them;
- cleanup runs for the outer scope, but a detached callback retained enough capability to mutate afterward.

## Advice

### What To Do Now

Repair cancellation as an ownership protocol, not as exception plumbing.

Start from the principal that can cancel: request, session, workflow, user command, parent task, or lifecycle scope. Thread one cancellation capability through every effect that remains owned by that principal. Where APIs use different mechanisms — abort controller, cancellation token, process signal, socket close, task group, session abort — adapt them at the boundary instead of silently dropping the signal.

### Why This Matters

The desired property is:

> **Once an owner withdraws authority, no effect it still owns may continue past the cancellation boundary.**

### Repair Strategy

If work truly needs to outlive the parent, transfer ownership before detach. A proper transfer usually needs:

- a new durable owner identity;
- a durable work record / queue item / job id;
- independent cancellation and retry policy;
- a destination for completion/failure;
- no remaining dependency on the vanished parent's in-memory scope.

### Decision Branches

That does not require every operation to stop at the same instruction. Some libraries only cancel at defined safe points. Some external services cannot recall a request already committed. The important part is that those semantics are explicit: after cancellation, either the effect is known not to happen, known to have happened, or its outcome is explicitly unknown and recovery handles that fact. “We returned Cancelled” is not enough.

### Common Wrong Fixes

Common fake repairs:

- `catch (Cancelled) { return }` while inner calls keep running;
- dropping a promise/future and assuming the underlying operation stopped;
- aborting the HTTP response but not the database/process/tool work triggered by it;
- cancelling only direct children while grandchildren continue;
- setting an `isCancelled` flag that inner effects never observe;
- ignoring late results while those results are still allowed to mutate shared or durable state;
- calling detached work “background” without naming a new owner;
- sending a process signal but never awaiting/confirming teardown, leaving resource lifetime ambiguous.

### Verification

Verification should cancel at **every meaningful phase**, not only immediately after start:

- before a child begins;
- while a network call is pending;
- while a process is running;
- after a child completed but before its result commits;
- during cleanup;
- after supersession by newer work.

Observe physical consequences: processes exit, sockets close, permits return, child sessions stop, no later callback mutates state, and no stale result is published after cancellation won.

For non-recallable external effects, test the uncertain-outcome branch explicitly. Cancellation cannot undo a charge already accepted by a remote service; it must not lie that “nothing happened.” Pair this rule with idempotency/reconciliation when effect acknowledgement can be lost.

### Done When

You are done when logical lifetime and physical lifetime agree, except at boundaries where ownership has been deliberately transferred or the protocol explicitly records an unknown/irreversible effect.

> “I stopped waiting” is not a cancellation guarantee. The question is what the owned work itself is still allowed to do.

### Nudge

> Cancellation is not an early return. It is a withdrawal of authority that must travel through the ownership graph.
