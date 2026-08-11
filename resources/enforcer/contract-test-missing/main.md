# contract-test-missing — Main

## What To Do Now
Add a contract-level test through the changed boundary and assert the exact representation, identity, ordering, and failure behavior the consumer relies on.

## Why This Matters
Unit tests prove local intent. A contract test proves compatibility between independent truths. Serialization details, process framing, provider IDs, transaction outcomes, and language conversions are often absent from the domain model yet decisive in production.

## Repair Strategy
Keep the fixture minimal and boundary-faithful. Prefer the real parser/serializer or adapter on both sides over hand-built approximations. Assert stable semantic properties and exact wire details only where those details are themselves contractual.

## Wrong Fixes
Do not mock both sides with the same mistaken assumption; that can make disagreement perfectly green. Do not test a private helper that bypasses the boundary transformation.

## Verification
Introduce a realistic incompatible change—wrong field, identity, framing, or error case. The contract test should fail before production can observe it.

## Done When
The changed boundary has an executable agreement that can detect either side drifting away from the contract the other side actually consumes.
