# retry-not-idempotent — Enforcer

## Definition
A retryable operation is non-idempotent when repeating the same logical request can create additional externally visible effects rather than converge to the same result.

## Governing Principle
Retries are temporal duplication. Networks and processes make retries inevitable whenever acknowledgements can be lost, so safe retry requires a stable logical identity that lets many physical attempts denote one operation. Without that identity, transport unreliability leaks into business semantics as duplicate charges, writes, prompts, publications, or resource creation.

## Trigger When
Trigger when code may retry an effectful operation and repeated execution can produce additional durable or externally visible effects.

## Do Not Trigger When
- The operation is read-only or naturally idempotent (PUT-by-key, set-once with the same value).
- Attempts are keyed by a stable idempotency identity that the receiver deduplicates.
- The client is explicitly configured never to retry after uncertain execution.
- The retry is a pure in-memory computation with no external effect.

## Distinguish From
optimistic-retry-assumption focuses on unknown outcome after a particular attempt. lost-update concerns overwrite of concurrent writes. This rule is the structural property that the operation itself is unsafe to execute more than once. Tie-break: fire here when a retry can create a second business effect; fire optimistic-retry-assumption when the code assumes an unknown attempt failed; fire lost-update when two different intents collide on one record.

## Decision Procedure
Execute the logical operation twice with the same stable request identity in thought/test. If the second attempt can create new business effect, add deduplication/idempotency or prohibit retry.

## Examples
- positive: a payment POST is retried on timeout and each attempt charges the card again.
- near-miss: a GET of account state is retried on 503; extra reads do not create effects.
- counterexample: the client sends `Idempotency-Key` allocated before the first attempt, and the server returns the original outcome on replay.

## Nudge
Retries multiply attempts; identity must collapse them back to one logical effect. Make the operation idempotent by stable key or do not retry it.
