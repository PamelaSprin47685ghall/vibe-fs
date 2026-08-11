# retry-not-idempotent — Enforcer

## Definition
A retryable operation is non-idempotent when repeating the same logical request can create additional externally visible effects rather than converge to the same result.

## Governing Principle
Retries are temporal duplication. Networks and processes make retries inevitable whenever acknowledgements can be lost, so safe retry requires a stable logical identity that lets many physical attempts denote one operation. Without that identity, transport unreliability leaks into business semantics as duplicate charges, writes, prompts, publications, or resource creation.

## Trigger When
Trigger when code may retry an effectful operation and repeated execution can produce additional durable or externally visible effects.

## Do Not Trigger When
Do not trigger when the operation is read-only, naturally idempotent, keyed by a stable idempotency identity, or explicitly configured never to retry after uncertain execution.

## Distinguish From
optimistic-retry-assumption focuses on unknown outcome after a particular attempt. This rule is the structural property that the operation itself is unsafe to execute more than once.

## Decision Procedure
Execute the logical operation twice with the same stable request identity in thought/test. If the second attempt can create new business effect, add deduplication/idempotency or prohibit retry.

## Nudge
Retries multiply attempts; identity must collapse them back to one logical effect. Make the operation idempotent by stable key or do not retry it.
