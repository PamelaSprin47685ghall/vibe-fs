# unbounded-fanout — Main

## What To Do Now
Introduce a concurrency limit (pool, semaphore, bounded map). Define cancel/backpressure when the bound is hit. Prefer streaming over loading all work into flight.

## Repair Strategy
Count worst-case spawn. Add a bound derived from resources. Propagate cancellation to children. Test overload behavior.

## Decision Branches
If work is hierarchical, bound each level. If external APIs rate-limit, align the bound with quotas.

## Wrong Fixes
Promise.all over unbounded user input. Spawning one agent per file in a huge tree. Relying on the OS to thrash as backpressure.

## Verification
Load tests show concurrency ≤ bound; cancel stops in-flight children; no runaway process growth.

## Done When
All fan-out paths have an explicit finite bound and cancellation policy.

## Scope and Authority
Concurrent spawn sites in runtime and agent orchestration.
