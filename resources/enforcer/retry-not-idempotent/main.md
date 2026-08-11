# retry-not-idempotent — Main

## What To Do Now
Give the logical operation a stable identity that survives physical retries, or make the operation explicitly non-retryable when repeated effects cannot be collapsed safely. The stable idempotency identity at the effect boundary is who owns collapsing retries into one intent; the retry loop is not who owns uniqueness.

## Why This Matters
A retry is not a second business intention; it is a second transport attempt to realize the first intention. If the receiver cannot recognize that equivalence, network uncertainty leaks into the domain as duplicated facts. The defect therefore is not “too many retries” but absence of an identity relation between attempts.

## Repair Strategy
Allocate the idempotency key before the first effect, persist or propagate it through every retry, and make the receiver deduplicate or return the original outcome for that key. For operations whose provider cannot support such semantics, refuse automatic retry after uncertain execution.

## Decision Branches
- If the provider supports idempotency keys, allocate the key before the first effect and reuse it on every retry.
- If the provider cannot collapse duplicates, disable retry after uncertain execution and surface the unknown outcome.
- If the operation is already naturally idempotent, keep retries and document the identity (same key, same body).

## Common Wrong Fixes
- Rely on short retry windows, low probability, or duplicate detection after the side effect has already escaped.
- Generate a new request id on each retry.
- Catch timeouts and “just POST again” without a key.
- Deduplicate only in logs or metrics, not at the effect boundary.

## Verification
Execute the same logical request multiple times under the same key, including after acknowledgement loss. Exactly one logical effect should remain observable. The invariant is: physical retries of one intent converge to one business effect.

## Done When
Transport may repeat attempts arbitrarily within policy, yet the business history contains one operation because physical repetition and logical identity are no longer confused.
