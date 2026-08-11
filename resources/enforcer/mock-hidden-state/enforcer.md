# mock-hidden-state — Enforcer

## Definition
A mock has hidden state when its response depends on an invisible cursor, request count, wall clock, mutable scenario phase, or prior call rather than on the provider-visible request and an explicit modeled state. The root-cause is that the double answers from an invisible cursor the protocol does not expose, so identical visible requests can yield different results.

## Governing Principle
A test double should make the contract easier to reason about, not invent an unseen universe behind it. Hidden mutable cursors create causality the production caller cannot observe: the same request returns different answers because the mock remembers something the protocol does not express. Tests then verify choreography with the fixture rather than semantics of the real interaction.

## Trigger When
Trigger when mock output changes according to call number, mutable closure state, suite order, hidden phase flags, or time despite equivalent visible requests.

## Do Not Trigger When
- The external protocol itself is stateful and that state is represented explicitly as part of the modeled server/session contract rather than as fixture magic.
- The double is a recorded cassette keyed by the visible request, with no extra cursor.
- Variation comes from explicit test input, not from mutable setup hidden in the mock.
- The double is a process-local fake whose state is constructed in the test body and passed as an explicit argument, not closed over inside the mock.

## Distinguish From
`order-dependent-test` concerns tests sharing residue. `test-implementation-coupled` concerns internal call expectations. Tie-break: if the mock's response function hides state the protocol does not expose, this rule; if tests leak state into each other, `order-dependent-test`; if the test asserts call choreography rather than the contract, `test-implementation-coupled`.

## Decision Procedure
For two identical visible requests in the same explicit modeled state, ask whether the mock can return different values. If yes, expose the missing state in the contract or remove the hidden dependency.

## Examples
- positive: The mock returns the next canned payload on each call regardless of the request body.
- near-miss: A fake session object exposes `open`/`balance` as protocol state and answers from that model.
- counterexample: A pure `request → response` map returns the same value for the same visible request.

## Nudge
A mock should be a transparent model of the provider contract. Make responses functions of visible request plus explicit protocol state, never of invisible test choreography.
