# retry-not-idempotent — Main

## What To Do Now
Give the logical operation a stable identity that survives physical retries, or make the operation explicitly non-retryable when repeated effects cannot be collapsed safely.

## Why This Matters
A retry is not a second business intention; it is a second transport attempt to realize the first intention. If the receiver cannot recognize that equivalence, network uncertainty leaks into the domain as duplicated facts. The defect therefore is not “too many retries” but absence of an identity relation between attempts.

## Repair Strategy
Allocate the idempotency key before the first effect, persist or propagate it through every retry, and make the receiver deduplicate or return the original outcome for that key. For operations whose provider cannot support such semantics, refuse automatic retry after uncertain execution.

## Wrong Fixes
Do not rely on short retry windows, low probability, or duplicate detection after the side effect has already escaped. Probability does not restore semantic identity.

## Verification
Execute the same logical request multiple times under the same key, including after acknowledgement loss. Exactly one logical effect should remain observable.

## Done When
Transport may repeat attempts arbitrarily within policy, yet the business history contains one operation because physical repetition and logical identity are no longer confused.
