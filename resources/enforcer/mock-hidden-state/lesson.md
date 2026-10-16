# mock-hidden-state — Lesson

## Opinion

### Definition

A mock has hidden state when its response depends on an invisible cursor, request count, wall clock, mutable scenario phase, or prior call rather than on the provider-visible request and an explicit modeled state. The root-cause is that the double answers from an invisible cursor the protocol does not expose, so identical visible requests can yield different results.

### Governing Principle

A test double should make the contract easier to reason about, not invent an unseen universe behind it. Hidden mutable cursors create causality the production caller cannot observe: the same request returns different answers because the mock remembers something the protocol does not express. Tests then verify choreography with the fixture rather than semantics of the real interaction.

### Trigger When

Trigger when mock output changes according to call number, mutable closure state, suite order, hidden phase flags, or time despite equivalent visible requests.

### Do Not Trigger When

- The external protocol itself is stateful and that state is represented explicitly as part of the modeled server/session contract rather than as fixture magic.
- The double is a recorded cassette keyed by the visible request, with no extra cursor.
- Variation comes from explicit test input, not from mutable setup hidden in the mock.
- The double is a process-local fake whose state is constructed in the test body and passed as an explicit argument, not closed over inside the mock.

### Distinguish From

`order-dependent-test` concerns tests sharing residue. `test-implementation-coupled` concerns internal call expectations. Tie-break: if the mock's response function hides state the protocol does not expose, this rule; if tests leak state into each other, `order-dependent-test`; if the test asserts call choreography rather than the contract, `test-implementation-coupled`.

### Decision Procedure

For two identical visible requests in the same explicit modeled state, ask whether the mock can return different values. If yes, expose the missing state in the contract or remove the hidden dependency.

### Examples

- positive: The mock returns the next canned payload on each call regardless of the request body.
- near-miss: A fake session object exposes `open`/`balance` as protocol state and answers from that model.
- counterexample: A pure `request → response` map returns the same value for the same visible request.

## Advice

### What To Do Now

Remove invisible cursors and call-count logic from the mock. Derive each response from the visible request and any protocol state the real provider actually exposes. The visible request plus that explicit protocol state is who owns the mock's answer, never call order or a hidden phase flag.

### Why This Matters

A stateful fixture can make tests pass only because they issue calls in the expected sequence. The production contract may contain no such sequence guarantee, so the test suite becomes coupled to its own choreography rather than to observable provider semantics.

### Repair Strategy

Model legitimate protocol state explicitly, keyed by stable identity when needed. Otherwise use a pure request → response function. Make scenario variation part of test input rather than mutable hidden setup.

### Decision Branches

- If the real provider is stateless for that request, make the mock a pure function of the visible request.
- If the protocol is stateful, model that state explicitly and key responses by it, never by call count.

### Common Wrong Fixes

- Reset the mock cursor more carefully between tests while keeping the invisible dependency inside each test.
- Add more canned answers in call order instead of binding them to requests.
- Assert call sequence on the mock to paper over the hidden cursor.

### Verification

Reorder equivalent independent calls and repeat identical requests. Results should change only when visible request or explicit protocol state changes. The invariant is that identical visible inputs in the same modeled state yield identical mock outputs.

### Done When

The mock can be understood as a small model of the external contract, not as a secret state machine driven by how the test happens to call it.

### Nudge

A mock should be a transparent model of the provider contract. Make responses functions of visible request plus explicit protocol state, never of invisible test choreography.
