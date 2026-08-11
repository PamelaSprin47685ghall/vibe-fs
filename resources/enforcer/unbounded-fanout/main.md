# unbounded-fanout — Main

## What To Do Now
Introduce a bounded map, worker pool, semaphore, or queue that caps active work independently of input size and propagates cancellation through the whole fan-out.

## Why This Matters
Parallel work competes for finite resources. If every input can become active immediately, a perfectly valid large input can turn into resource exhaustion, provider overload, or self-inflicted denial of service. A bound makes capacity a deliberate invariant rather than an emergent property of workload size.

## Repair Strategy
Choose the resource being protected, set the active bound from its capacity/SLO, preserve deterministic result association, and stop or drain queued work according to explicit cancellation/failure policy.

## Wrong Fixes
Do not choose an enormous constant merely to silence the rule. The number should correspond to a finite resource or known safe service envelope.

## Verification
Exercise inputs much larger than the concurrency bound. Active work must remain capped, queued work must not leak after cancellation, and logical results must not depend on completion order.

## Done When
The system can accept arbitrarily larger workloads without translating their cardinality directly into arbitrarily larger simultaneous resource demand.
