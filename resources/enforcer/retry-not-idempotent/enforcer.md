# retry-not-idempotent — Enforcer

## Definition
A retryable operation can duplicate writes, prompts, publications, charges, processes, or resource creation when run more than once.

## Trigger When
A retryable operation can duplicate writes, prompts, publications, charges, processes, or resource creation.

## Do Not Trigger When
Do not fire when the operation is already idempotent by stable key, or retries are disabled for non-idempotent effects.

## Distinguish From
optimistic-retry-assumption assumes success semantics; this tip is specifically duplicate side effects under retry.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A retryable effect is not idempotent. Add a stable identity and prove repeated execution is safe.

## Examples
### Positive
A retryable operation can duplicate writes, prompts, publications, charges, processes, or resource creation.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when the operation is already idempotent by stable key, or retries are disabled for non-idempotent effects.
