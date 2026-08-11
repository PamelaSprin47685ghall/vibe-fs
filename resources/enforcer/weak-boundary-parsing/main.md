# weak-boundary-parsing — Main

## What To Do Now
Parse and validate external data once at ingress, then expose a normalized strong internal type instead of passing raw payload shape deeper into the system. Repair at the adapter: who owns ingress owns the conversion from payload to domain type.

## Why This Matters
Repeated parsing means repeated uncertainty. Each downstream layer knows less about provenance yet must reconstruct the same distinctions, so validation drifts and malformed states travel farther before failing. A boundary should exchange ambiguity for guarantees.

## Repair Strategy
Decode schema/version, validate required relations, normalize units/names, and construct domain values at the adapter. Keep raw protocol forms private unless a lower layer is explicitly the protocol owner.

## Decision Branches
If raw or loosely typed payload is visible past ingress, parse and construct the strong type at the adapter.
If a layer is the protocol owner (signatures, framing), keep raw bytes there and still emit the domain type before other code runs.

## Common Wrong Fixes
- Scatter helper predicates such as `hasField` or `isValidShape` across consumers.
- Validate in the controller and still pass the original map inward.
- Treat a TypeScript interface on `any` JSON as parsing.

## Verification
Invariant: the system crosses from weak external representation to strong internal meaning at one identifiable boundary. Malformed and unsupported payloads should fail at ingress with typed outcomes; valid payloads should require no repeated shape checks once inside.

## Done When
The system crosses from weak external representation to strong internal meaning at one identifiable boundary and never has to rediscover that meaning downstream.
