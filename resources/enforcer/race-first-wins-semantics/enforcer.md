# race-first-wins-semantics — Enforcer

## Definition
Race-first-wins semantics arise when scheduler timing or whichever concurrent operation finishes first determines a business result even though the competing operations carry different information.

## Governing Principle
Scheduling order is usually an accident of load, network, and runtime, not a domain fact. If “first completion” chooses truth, identical logical inputs can produce different outcomes under different timing. The system has then delegated business semantics to the scheduler. Determinism requires either an explicit first-writer rule with stable identity or a merge function over the complete relevant information.

## Trigger When
Trigger when concurrent requests/results race and the first observed completion becomes authoritative despite no domain rule saying temporal arrival order should decide.

## Do Not Trigger When
Do not trigger when first-writer-wins, lowest-latency replica, election timeout, or another timing rule is itself the documented protocol and carries the necessary identity/quorum semantics.

## Distinguish From
shared-mutable-concurrency concerns coordination through shared state. lost-update concerns overwrite conflicts. This rule concerns scheduler order becoming domain meaning.

## Decision Procedure
Ask whether swapping completion order while keeping logical inputs identical should change the result. If not, collect required results and merge using a deterministic rule independent of timing.

## Nudge
Do not let the scheduler invent business truth. Either make arrival order an explicit domain rule or derive the result from complete information with deterministic merge semantics.
