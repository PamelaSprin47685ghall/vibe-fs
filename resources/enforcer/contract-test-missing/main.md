# contract-test-missing — Main

## What To Do Now
Add a contract-level test through the changed boundary and assert the exact representation, identity, ordering, and failure behavior the consumer relies on. The inter-system boundary is who owns the observable agreement; each side’s unit tests are not who owns compatibility between them.

## Why This Matters
Unit tests prove local intent. A contract test proves compatibility between independent truths. Serialization details, process framing, provider IDs, transaction outcomes, and language conversions are often absent from the domain model yet decisive in production.

## Repair Strategy
Keep the fixture minimal and boundary-faithful. Prefer the real parser/serializer or adapter on both sides over hand-built approximations. Assert stable semantic properties and exact wire details only where those details are themselves contractual.

## Decision Branches
- If an inter-system boundary changed and no test exercises the agreement, add that contract test first.
- If an equivalent contract test already fails on incompatibility for this behavior, do not add a second theater suite.
- If only internals changed and the observable agreement is untouched, do not invent a new contract test for the same bytes.

## Common Wrong Fixes
- Do not mock both sides with the same mistaken assumption.
- Do not test a private helper that bypasses the boundary transformation.
- Do not assert only that serialization “does not throw.”
- Do not copy production payloads into snapshots without stating which fields are contractual.

## Verification
Introduce a realistic incompatible change—wrong field, identity, framing, or error case. The contract test should fail before production can observe it. The invariant is that the changed boundary has an executable agreement that detects either side drifting from what the other consumes.

## Done When
The changed boundary has an executable agreement that can detect either side drifting away from the contract the other side actually consumes.
