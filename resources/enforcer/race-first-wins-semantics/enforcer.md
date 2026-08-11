# race-first-wins-semantics — Enforcer

## Definition
Race-first-wins semantics arise when scheduler timing or whichever concurrent operation finishes first determines a business result even though the competing operations carry different information. The root-cause is that scheduler arrival order is treated as domain meaning, so identical logical inputs can yield different business results under different timing.

## Governing Principle
Scheduling order is usually an accident of load, network, and runtime, not a domain fact. If “first completion” chooses truth, identical logical inputs can produce different outcomes under different timing. The system has then delegated business semantics to the scheduler. Determinism requires either an explicit first-writer rule with stable identity or a merge function over the complete relevant information.

## Trigger When
Trigger when concurrent requests/results race and the first observed completion becomes authoritative despite no domain rule saying temporal arrival order should decide.

## Do Not Trigger When
- First-writer-wins, lowest-latency replica, election timeout, or another timing rule is itself the documented protocol and carries the necessary identity/quorum semantics.
- Concurrency is used only to fetch inputs, then a deterministic join/merge decides after all required results arrive.
- A single-owner queue serializes commands so completion order cannot choose among competing payloads.
- The “winner” is selected by an explicit domain key (version, timestamp field, priority), not by which future resolved first.

## Distinguish From
shared-mutable-concurrency concerns coordination through shared state. lost-update concerns overwrite conflicts. This rule concerns scheduler order becoming domain meaning. Tie-break: fire here when arrival order invents the answer; fire shared-mutable-concurrency when several writers share mutation authority; fire lost-update when a later write silently overwrites without merge.

## Decision Procedure
Ask whether swapping completion order while keeping logical inputs identical should change the result. If not, collect required results and merge using a deterministic rule independent of timing.

## Examples
- positive: two price quotes race and the first HTTP response becomes the booked price even though both quotes are valid inputs to a documented min/max merge.
- near-miss: a leader-election timeout is specified as the protocol; the first heartbeat after the timeout is the documented winner.
- counterexample: a map-reduce job waits for every shard, then folds with an associative reducer; scheduler order does not change the result.

## Nudge
Do not let the scheduler invent business truth. Either make arrival order an explicit domain rule or derive the result from complete information with deterministic merge semantics.
