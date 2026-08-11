# blocking-event-loop — Enforcer

## Definition
An event loop is blocked when one task retains the loop while waiting on work whose completion does not require the loop’s exclusive attention. The root-cause is that a task keeps exclusive hold of the shared event-loop thread while waiting on work that does not need that exclusivity, turning one local stall into global head-of-line blocking.

## Governing Principle
An event loop buys concurrency by making a strict bargain: each callback may borrow the thread briefly, never own it while the world is slow. Blocking I/O, sleeps, synchronous process waits, and long CPU loops break that bargain. One local wait becomes global head-of-line blocking because unrelated progress shares the same executor.

## Trigger When
Trigger when synchronous filesystem, process, network, sleep, lock wait, or CPU-heavy work runs on an event-loop, hook, UI, or reactor thread.

## Do Not Trigger When
- The work is bounded trivial computation whose worst-case latency is demonstrably below the loop’s service budget.
- The wait already uses the loop’s native non-blocking I/O and returns the thread while pending.
- The heavy work already runs on a dedicated worker, and the loop only receives the completion.
- A test harness that is not a shared production event loop is allowed to block its own private thread.

## Distinguish From
`serial-when-parallel` wastes available concurrency. `sleep-based-synchronization` uses delay as causality. This rule concerns monopolizing the shared progress engine itself. Tie-break: if unrelated work cannot progress because this task still owns the loop thread, this rule owns the case.

## Decision Procedure
1. Identify the thread or executor.
2. Determine whether other work depends on its prompt return.
3. Bound the operation’s worst-case duration.
4. Move unbounded waits or heavy computation behind async I/O or a worker boundary.

## Examples
- positive: a request handler calls synchronous `readFileSync` or `sleep` on the shared event-loop thread.
- near-miss: a few microseconds of pure JSON parse on the loop, within a measured service budget.
- counterexample: wait with async I/O, and move CPU-heavy work to a bounded worker that does not own the loop.

## Nudge
The loop is a scheduler, not a workplace. Return it quickly; move blocking or heavy work to a boundary that can wait without freezing unrelated progress.
