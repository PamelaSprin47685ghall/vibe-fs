# unbounded-fanout — Lesson

## Opinion

### Definition

Fan-out is unbounded when work creation follows input size without a finite concurrency limit, allowing tasks, requests, processes, agents, or file operations to demand resources faster than the system can service them.

### Governing Principle

Concurrency converts a collection into simultaneous claims on finite resources. The root-cause is treating input cardinality as concurrency policy. Without a bound, a larger list means more sockets, memory, file descriptors, processes, or remote pressure with no independent ceiling. Bounded concurrency separates two quantities that must not be confused—the amount of work that exists and the amount of work allowed to be active now.

### Trigger When

Trigger when code spawns or starts one concurrent operation per item with no semaphore, worker pool, bounded map, queue capacity, or inherently small fixed upper bound.

### Do Not Trigger When

- The fan-out is statically tiny and documented (a fixed handful of known children).
- An existing mechanism enforces a finite active-work limit with cancellation semantics.
- Work is processed sequentially or through a pool whose size is independent of input cardinality.
- The “fan-out” is pure CPU iteration with no extra concurrent resource claims.

### Distinguish From

`serial-when-parallel` underuses available concurrency. `resource-not-scoped` concerns lifetime after acquisition. Tie-break: if simultaneous demand has no upper bound, use this rule; if work that could be parallel is forced serial, use `serial-when-parallel`.

### Decision Procedure

Identify the finite resource exhausted by concurrency, choose an explicit safe active bound, and define what happens to queued work when the parent fails or cancels.

### Examples

- positive: `items.map(i => fetch(i))` awaits all at once with no concurrency cap.
- near-miss: a worker pool of size 8 pulls from a queue of 10,000 jobs with cancellation on parent abort.
- counterexample: a pipeline that could fetch in parallel but loops `await` one-by-one is `serial-when-parallel`.

## Advice

### What To Do Now

Introduce a bounded map, worker pool, semaphore, or queue that caps active work independently of input size and propagates cancellation through the whole fan-out. Repair at the scheduler: who owns the finite resource owns the active-work ceiling.

### Why This Matters

Parallel work competes for finite resources. If every input can become active immediately, a perfectly valid large input can turn into resource exhaustion, provider overload, or self-inflicted denial of service. A bound makes capacity a deliberate invariant rather than an emergent property of workload size.

### Repair Strategy

Choose the resource being protected, set the active bound from its capacity/SLO, preserve deterministic result association, and stop or drain queued work according to explicit cancellation/failure policy.

### Decision Branches

If active work scales with input size, add a finite bound and a cancellation/drain policy.
If a bound already exists and cancellation is defined, keep it and do not spawn extra unbounded children beside it.

### Common Wrong Fixes

- Choose an enormous constant merely to silence the rule.
- Bound only in tests while production still fans out 1:1.
- Fire tasks without joining or cancelling, so the “bound” is only on the spawning loop.

### Verification

Invariant: active work remains capped independent of input cardinality. Exercise inputs much larger than the bound; queued work must not leak after cancellation, and logical results must not depend on completion order.

### Done When

The system can accept arbitrarily larger workloads without translating their cardinality directly into arbitrarily larger simultaneous resource demand.

### Nudge

Input size is not a concurrency policy. Separate total work from active work with a finite bound, and make cancellation of queued and running children explicit.
