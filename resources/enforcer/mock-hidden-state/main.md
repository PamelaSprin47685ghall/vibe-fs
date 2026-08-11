# mock-hidden-state — Main

## What To Do Now
Remove invisible cursors and call-count logic from the mock. Derive each response from the visible request and any protocol state the real provider actually exposes. The visible request plus that explicit protocol state is who owns the mock's answer, never call order or a hidden phase flag.

## Why This Matters
A stateful fixture can make tests pass only because they issue calls in the expected sequence. The production contract may contain no such sequence guarantee, so the test suite becomes coupled to its own choreography rather than to observable provider semantics.

## Repair Strategy
Model legitimate protocol state explicitly, keyed by stable identity when needed. Otherwise use a pure request → response function. Make scenario variation part of test input rather than mutable hidden setup.

## Decision Branches
- If the real provider is stateless for that request, make the mock a pure function of the visible request.
- If the protocol is stateful, model that state explicitly and key responses by it, never by call count.

## Common Wrong Fixes
- Reset the mock cursor more carefully between tests while keeping the invisible dependency inside each test.
- Add more canned answers in call order instead of binding them to requests.
- Assert call sequence on the mock to paper over the hidden cursor.

## Verification
Reorder equivalent independent calls and repeat identical requests. Results should change only when visible request or explicit protocol state changes. The invariant is that identical visible inputs in the same modeled state yield identical mock outputs.

## Done When
The mock can be understood as a small model of the external contract, not as a secret state machine driven by how the test happens to call it.
