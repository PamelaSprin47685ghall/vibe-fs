# mock-hidden-state — Enforcer

## Definition
A mock has hidden state when its response depends on an invisible cursor, request count, wall clock, mutable scenario phase, or prior call rather than on the provider-visible request and an explicit modeled state.

## Governing Principle
A test double should make the contract easier to reason about, not invent an unseen universe behind it. Hidden mutable cursors create causality the production caller cannot observe: the same request returns different answers because the mock remembers something the protocol does not express. Tests then verify choreography with the fixture rather than semantics of the real interaction.

## Trigger When
Trigger when mock output changes according to call number, mutable closure state, suite order, hidden phase flags, or time despite equivalent visible requests.

## Do Not Trigger When
Do not trigger when the external protocol itself is stateful and that state is represented explicitly as part of the modeled server/session contract rather than as fixture magic.

## Distinguish From
order-dependent-test concerns tests sharing residue. test-implementation-coupled concerns internal call expectations. This rule is hidden state inside the mock’s response function.

## Decision Procedure
For two identical visible requests in the same explicit modeled state, ask whether the mock can return different values. If yes, expose the missing state in the contract or remove the hidden dependency.

## Nudge
A mock should be a transparent model of the provider contract. Make responses functions of visible request plus explicit protocol state, never of invisible test choreography.
