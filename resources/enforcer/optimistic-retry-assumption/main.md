# optimistic-retry-assumption — Main

## What To Do Now
Treat an unknown external outcome as its own state. Resolve it through idempotency identity, status lookup, deduplication, or an explicit at-most-once protocol before repeating the effect.

## Why This Matters
A timeout removes knowledge, not history. If the remote side committed before the response vanished, an unqualified retry can duplicate charges, prompts, publications, resource creation, or writes while both attempts appear individually successful.

## Repair Strategy
Assign stable operation identity before the first attempt and carry it through retries. Where the provider lacks idempotency, design recovery around querying authoritative state or refusing automatic retry when duplication cannot be ruled out.

## Decision Branches
- If the outcome is unknown and the effect may have committed, resolve via identity or status lookup before any repeat.
- If failure is known to precede the effect, or the op is read-only/idempotent, retry is not this defect.

## Common Wrong Fixes
- Assume "no response = no effect."
- Add exponential backoff as if delay changed duplication semantics.
- Retry with a fresh identity, guaranteeing a second logical effect.

## Verification
Simulate "effect committed, acknowledgement lost." Recovery must converge to one logical effect rather than issue an indistinguishable second one. The invariant is that unknown is not treated as failure-before-effect.

## Done When
Every retry after uncertainty is semantically safe because the system can prove whether repeated execution denotes the same logical operation.
