# cancellation-not-propagated — Enforcer

## Definition
Cancellation is broken when an operation acknowledges that its owner no longer wants the result but leaves owned child work running beyond that decision.

## Governing Principle
Cancellation is a statement about ownership, not a cosmetic early return. If a parent may abandon its result while children continue consuming sockets, processes, permits, or money, the runtime lifetime has escaped the logical lifetime. The system then contains work with no remaining principal—effects whose owner has ceased to exist.

## Trigger When
Trigger when an abort signal, cancellation token, timeout, disconnect, or supersession stops at an outer layer while inner network calls, processes, tools, agents, streams, or workers continue.

## Do Not Trigger When
Do not trigger for deliberately detached work whose independent ownership, durability, and completion semantics are explicit before the parent exits.

## Distinguish From
resource-not-scoped concerns acquire/release lifetime generally. permit-leak concerns concurrency capacity. This rule concerns propagation of the parent’s decision that owned work should cease.

## Decision Procedure
Trace the ownership tree from the cancelled operation to every child effect. For each child, identify how cancellation reaches it and what cleanup is guaranteed afterward.

## Nudge
Cancellation must follow ownership all the way down. Either propagate it through every owned effect or explicitly transfer ownership before detaching the work.
