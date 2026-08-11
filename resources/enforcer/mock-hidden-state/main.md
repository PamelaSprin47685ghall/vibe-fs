# mock-hidden-state — Main

## What To Do Now
Remove invisible cursors and call-count logic from the mock. Derive each response from the visible request and any protocol state the real provider actually exposes.

## Why This Matters
A stateful fixture can make tests pass only because they issue calls in the expected sequence. The production contract may contain no such sequence guarantee, so the test suite becomes coupled to its own choreography rather than to observable provider semantics.

## Repair Strategy
Model legitimate protocol state explicitly, keyed by stable identity when needed. Otherwise use a pure request → response function. Make scenario variation part of test input rather than mutable hidden setup.

## Wrong Fixes
Do not reset the mock cursor more carefully between tests. Better cleanup preserves the same invisible dependency inside each test.

## Verification
Reorder equivalent independent calls and repeat identical requests. Results should change only when visible request or explicit protocol state changes.

## Done When
The mock can be understood as a small model of the external contract, not as a secret state machine driven by how the test happens to call it.
