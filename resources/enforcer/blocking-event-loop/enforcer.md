# blocking-event-loop — Enforcer

## Definition
An event loop is blocked when one task retains the loop while waiting on work whose completion does not require the loop’s exclusive attention.

## Governing Principle
An event loop buys concurrency by making a strict bargain: each callback may borrow the thread briefly, never own it while the world is slow. Blocking I/O, sleeps, synchronous process waits, and long CPU loops break that bargain. One local wait becomes global head-of-line blocking because unrelated progress shares the same executor.

## Trigger When
Trigger when synchronous filesystem, process, network, sleep, lock wait, or CPU-heavy work runs on an event-loop, hook, UI, or reactor thread.

## Do Not Trigger When
Do not trigger for bounded trivial computation whose worst-case latency is demonstrably below the loop’s service budget.

## Distinguish From
serial-when-parallel wastes available concurrency. sleep-based-synchronization uses delay as causality. This rule concerns monopolizing the shared progress engine itself.

## Decision Procedure
1. Identify the thread or executor.
2. Determine whether other work depends on its prompt return.
3. Bound the operation’s worst-case duration.
4. Move unbounded waits or heavy computation behind async I/O or a worker boundary.

## Nudge
The loop is a scheduler, not a workplace. Return it quickly; move blocking or heavy work to a boundary that can wait without freezing unrelated progress.
