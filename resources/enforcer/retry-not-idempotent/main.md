# retry-not-idempotent — Main

## What To Do Now
Give the effect a stable idempotency key or natural identity. Make handlers safe under at-least-once delivery. Prove double execution does not double-apply.

## Repair Strategy
Introduce dedupe keys, upsert semantics, or outbox with unique constraints. Separate "accept command" from "apply effect" when needed.

## Decision Branches
If the effect cannot be made idempotent, remove automatic retry and require explicit operator replay with safeguards. If only parts are safe, retry only the safe segment.

## Wrong Fixes
Blind HTTP retries on POST that creates records. Re-sending prompts that charge tokens without dedupe. Catch-and-retry around multi-step side effects.

## Verification
Execute the path twice with the same identity; observable side effects remain single-applied.

## Done When
Retry policy only wraps idempotent effects, or identity-based dedupe is proven.

## Scope and Authority
Effects under retry/at-least-once delivery. Not pure CPU recomputation.
