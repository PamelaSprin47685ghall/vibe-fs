# blocking-event-loop — Main

## What To Do Now
Remove blocking waits and long-running computation from the event-loop thread. Use native asynchronous APIs for waiting and a bounded worker mechanism for CPU or unavoidable blocking work.

## Why This Matters
The loop is a shared liveness resource. Latency introduced there is multiplied by every unrelated operation queued behind it. A five-second synchronous wait is therefore not one five-second delay; it is five seconds during which the system has surrendered its ability to make independent progress.

## Repair Strategy
Separate parsing, pure computation, and dispatch from slow effects. Keep each turn small. Preserve cancellation and error semantics across the new boundary instead of merely wrapping a blocking call in an async-looking function.

## Wrong Fixes
Do not increase timeouts, add sleeps, or call blocking work from an async wrapper on the same executor. Syntax does not create concurrency; ownership of the waiting thread does.

## Verification
Exercise the slow path while unrelated work is active. The loop must remain responsive and cancellation must still reach the displaced work.

## Done When
No unbounded wait or heavy computation can monopolize the shared event-loop executor, and unrelated tasks continue to make progress while slow work is pending.
