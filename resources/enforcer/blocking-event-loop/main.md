# blocking-event-loop — Main

## What To Do Now
Remove blocking waits and long-running computation from the event-loop thread. Use native asynchronous APIs for waiting and a bounded worker mechanism for CPU or unavoidable blocking work. The event-loop executor is who owns the liveness invariant that each turn returns promptly; blocking I/O and heavy CPU belong on a worker, not on that loop.

## Why This Matters
The loop is a shared liveness resource. Latency introduced there is multiplied by every unrelated operation queued behind it. A five-second synchronous wait is therefore not one five-second delay; it is five seconds during which the system has surrendered its ability to make independent progress.

## Repair Strategy
Separate parsing, pure computation, and dispatch from slow effects. Keep each turn small. Preserve cancellation and error semantics across the new boundary instead of merely wrapping a blocking call in an async-looking function.

## Decision Branches
- If the work is waiting on I/O, use the native async API so the loop thread is released while pending.
- If the work is CPU-heavy or unavoidably blocking, move it to a bounded worker and keep the loop as the completion dispatcher.
- If an `async` wrapper still calls blocking APIs on the same executor, treat it as still blocking.

## Common Wrong Fixes
- Do not increase timeouts so the blocked loop appears healthy.
- Do not add sleeps to “yield” while still holding the loop.
- Do not wrap blocking work in `async` on the same executor and call it concurrency.
- Do not drop cancellation or error mapping when moving work off the loop.

## Verification
Exercise the slow path while unrelated work is active. The loop must remain responsive and cancellation must still reach the displaced work. The invariant is that no unbounded wait or heavy computation monopolizes the shared event-loop executor.

## Done When
No unbounded wait or heavy computation can monopolize the shared event-loop executor, and unrelated tasks continue to make progress while slow work is pending.
