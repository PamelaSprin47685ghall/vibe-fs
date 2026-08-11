# primitive-obsession — Main

## What To Do Now
Introduce a distinct domain type at the boundary and migrate callers so values with the same primitive representation but different meanings are no longer interchangeable.

## Why This Matters
A primitive tells the compiler how bits are stored, not what they mean. When semantic categories collapse into one representation, the compiler cannot help with one of the most valuable classes of mistakes: a valid-looking value supplied to the wrong concept.

## Repair Strategy
Wrap or define the concept with its validation and operations, keep conversion at explicit ingress/egress boundaries, and avoid exposing the raw primitive again across domain APIs.

## Wrong Fixes
Do not merely rename parameters `accountId` and `orderId` while both remain strings; humans see the distinction, the type checker still does not.

## Verification
Attempt to pass a sibling concept with the same underlying primitive. The program should reject the substitution at compile/construction time.

## Done When
The boundary carries domain identity as part of the type, invalid substitutions are unrepresentable, and conversion to raw primitives is confined to adapters that genuinely require it.
