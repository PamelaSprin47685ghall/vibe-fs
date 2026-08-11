# optimistic-retry-assumption — Enforcer

## Definition
An optimistic retry assumption exists when an external effect with unknown outcome is repeated without an identity or protocol that makes repetition safe.

## Governing Principle
Timeout means "knowledge is missing," not "the effect did not happen." The remote system may have committed just before the response was lost. Retrying under that uncertainty creates two possible histories—one effect or two—unless stable identity collapses them or an at-most-once protocol can query/resolve the original attempt.

## Trigger When
Trigger when network/process/tool calls that may create irreversible side effects are retried after timeout, disconnect, crash, or unknown result without idempotency identity or explicit recovery semantics.

## Do Not Trigger When
- The operation is proven idempotent, uniquely keyed, read-only, or covered by a protocol that can determine the original outcome before any repeat.
- Failure is known to have occurred before the effect (validation rejected, connection never established).
- The caller records unknown and requires explicit human/protocol resolution rather than automatic retry.

## Distinguish From
retry-not-idempotent concerns repeatability of a known retryable operation. partial-write-assumption invents storage states. Tie-break: if the issue is epistemic uncertainty after an external effect may already have happened, this rule; if retries of an operation already deemed retryable are not idempotent, retry-not-idempotent; if recovery invents unobservable storage states, partial-write-assumption.

## Decision Procedure
Classify outcomes as known success, known failure-before-effect, or unknown. Only the unknown case needs a recovery protocol; never silently reinterpret it as failure.

## Examples
- positive: After a payment HTTP timeout the client posts the same charge again with a new request id.
- near-miss: A GET of account status is retried; the operation is read-only.
- counterexample: The client sends a stable idempotency key and the provider deduplicates before a second charge.

## Nudge
Unknown is not failure. Before retrying a possibly committed external effect, establish stable identity or a protocol that can resolve/reuse the original attempt safely.
