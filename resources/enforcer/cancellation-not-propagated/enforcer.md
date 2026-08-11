# cancellation-not-propagated — Enforcer

## Definition
Cancellation is broken when an operation acknowledges that its owner no longer wants the result but leaves owned child work running beyond that decision. The root-cause is that cancellation is treated as an outer return rather than a statement about ownership, so owned child effects keep running after the principal has ceased.

## Governing Principle
Cancellation is a statement about ownership, not a cosmetic early return. If a parent may abandon its result while children continue consuming sockets, processes, permits, or money, the runtime lifetime has escaped the logical lifetime. The system then contains work with no remaining principal—effects whose owner has ceased to exist.

## Trigger When
Trigger when an abort signal, cancellation token, timeout, disconnect, or supersession stops at an outer layer while inner network calls, processes, tools, agents, streams, or workers continue.

## Do Not Trigger When
- The work is deliberately detached, with independent ownership, durability, and completion semantics explicit before the parent exits.
- Cancellation is fully threaded and child effects stop, with cleanup guaranteed, when the parent aborts.
- A completed child whose result is already durable is not “still running” merely because the parent later cancels a later stage.
- Observability logs that outlive the request are not owned child work.

## Distinguish From
`resource-not-scoped` concerns acquire/release lifetime generally. `permit-leak` concerns concurrency capacity. This rule concerns propagation of the parent’s decision that owned work should cease. Tie-break: if the parent has cancelled but owned children still run, this rule owns the case even when resources would eventually be released.

## Decision Procedure
Trace the ownership tree from the cancelled operation to every child effect. For each child, identify how cancellation reaches it and what cleanup is guaranteed afterward.

## Examples
- positive: an HTTP timeout returns cancelled while an inner tool process and outbound request keep running.
- near-miss: a durable outbox job is explicitly transferred to a background owner before the request ends.
- counterexample: the abort signal is threaded through every owned child, and those children stop with cleanup when cancellation wins.

## Nudge
Cancellation must follow ownership all the way down. Either propagate it through every owned effect or explicitly transfer ownership before detaching the work.
