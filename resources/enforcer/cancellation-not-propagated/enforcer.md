# cancellation-not-propagated — Enforcer

Cancellation is broken when the outer operation says “this work is over” while work that still belongs to that operation keeps running.

That is not merely a resource leak. It is a lie about ownership.

A request timeout, user abort, superseding command, session shutdown, or cancelled task means some principal has withdrawn authority for the work it owns. If child processes, network calls, agent runs, streams, database operations, timers, or callbacks continue after that point without an explicit ownership transfer, the runtime lifetime has escaped the logical lifetime.

The result is orphan work: effects with no remaining principal that is entitled to want them.

This is where some of the nastiest “impossible” incidents come from:

- the UI says Cancelled, then a stale request writes state thirty seconds later;
- a timed-out tool process keeps holding a file or port;
- a superseded background computation publishes its result after a newer computation already won;
- a client disconnect stops response handling but downstream billing/API work continues;
- a parent agent is aborted while child sessions keep consuming tokens and later report into a world that no longer expects them;
- cleanup runs for the outer scope, but a detached callback retained enough capability to mutate afterward.

Fire this rule when cancellation/abort is observed at one layer but an owned child effect has no causal path to that signal.

Do not fire for truly detached work whose ownership was transferred **before** the parent returned. A durable outbox job, queue item, scheduler task, or independently owned workflow may legitimately outlive the initiating request. But detachment is not “we stopped awaiting it.” Real transfer answers: who owns it now, where is that ownership durable, who can cancel it, and who receives its completion.

Also do not confuse “result ignored” with “work cancelled.” Dropping the future/promise only stops *you* from listening. It says nothing about whether the external work stopped.

Nearby rules:

- `resource-not-scoped` — acquire/release lifetime is structurally unsafe;
- `permit-leak` — bounded concurrency capacity is never returned;
- `race-first-wins-semantics` — stale/losing work may continue, but the central defect is timing choosing truth;
- `partial-write-assumption` — cancellation may interrupt a write, but that rule concerns assuming no partial effect occurred.

The key diagnostic is an ownership tree. Start at the cancelled operation and enumerate every child effect it caused. For each one, ask:

1. does the child still belong to this parent at cancellation time?
2. if yes, how does the cancellation signal reach it?
3. what proves its physical effect stopped or reached a defined cancellation boundary?
4. what cleanup is guaranteed?
5. if no, where exactly was ownership transferred?

If any answer is “we just stop awaiting it,” the work is not cancelled. It is abandoned while still armed.

> Cancellation is not an early return. It is a withdrawal of authority that must travel through the ownership graph.
