# blocking-event-loop — Lesson

## Opinion

### Definition

An event loop is blocked when one task retains the loop while waiting on work whose completion does not require the loop’s exclusive attention. The root-cause is that a task keeps exclusive hold of the shared event-loop thread while waiting on work that does not need that exclusivity, turning one local stall into global head-of-line blocking.

### Governing Principle

An event loop buys concurrency by making a strict bargain: each callback may borrow the thread briefly, never own it while the world is slow. Blocking I/O, sleeps, synchronous process waits, and long CPU loops break that bargain. One local wait becomes global head-of-line blocking because unrelated progress shares the same executor.

### Trigger When

Trigger when synchronous filesystem, process, network, sleep, lock wait, or CPU-heavy work runs on an event-loop, hook, UI, or reactor thread.

### Do Not Trigger When

- The work is bounded trivial computation whose worst-case latency is demonstrably below the loop’s service budget.
- The wait already uses the loop’s native non-blocking I/O and returns the thread while pending.
- The heavy work already runs on a dedicated worker, and the loop only receives the completion.
- A test harness that is not a shared production event loop is allowed to block its own private thread.

### Distinguish From

`serial-when-parallel` wastes available concurrency. `sleep-based-synchronization` uses delay as causality. This rule concerns monopolizing the shared progress engine itself. Tie-break: if unrelated work cannot progress because this task still owns the loop thread, this rule owns the case.

### Decision Procedure

1. Identify the thread or executor.
2. Determine whether other work depends on its prompt return.
3. Bound the operation’s worst-case duration.
4. Move unbounded waits or heavy computation behind async I/O or a worker boundary.

### Examples

- positive: a request handler calls synchronous `readFileSync` or `sleep` on the shared event-loop thread.
- near-miss: a few microseconds of pure JSON parse on the loop, within a measured service budget.
- counterexample: wait with async I/O, and move CPU-heavy work to a bounded worker that does not own the loop.

## Advice

### What To Do Now

Remove blocking waits and long-running computation from the event-loop thread. Use native asynchronous APIs for waiting and a bounded worker mechanism for CPU or unavoidable blocking work. The event-loop executor is who owns the liveness invariant that each turn returns promptly; blocking I/O and heavy CPU belong on a worker, not on that loop.

### Why This Matters

The loop is a shared liveness resource. Latency introduced there is multiplied by every unrelated operation queued behind it. A five-second synchronous wait is therefore not one five-second delay; it is five seconds during which the system has surrendered its ability to make independent progress.

### Repair Strategy

Separate parsing, pure computation, and dispatch from slow effects. Keep each turn small. Preserve cancellation and error semantics across the new boundary instead of merely wrapping a blocking call in an async-looking function.

### Decision Branches

- If the work is waiting on I/O, use the native async API so the loop thread is released while pending.
- If the work is CPU-heavy or unavoidably blocking, move it to a bounded worker and keep the loop as the completion dispatcher.
- If an `async` wrapper still calls blocking APIs on the same executor, treat it as still blocking.

### Common Wrong Fixes

- Do not increase timeouts so the blocked loop appears healthy.
- Do not add sleeps to “yield” while still holding the loop.
- Do not wrap blocking work in `async` on the same executor and call it concurrency.
- Do not drop cancellation or error mapping when moving work off the loop.

### Verification

Exercise the slow path while unrelated work is active. The loop must remain responsive and cancellation must still reach the displaced work. The invariant is that no unbounded wait or heavy computation monopolizes the shared event-loop executor.

### Done When

No unbounded wait or heavy computation can monopolize the shared event-loop executor, and unrelated tasks continue to make progress while slow work is pending.

### Nudge

The loop is a scheduler, not a workplace. Return it quickly; move blocking or heavy work to a boundary that can wait without freezing unrelated progress.
