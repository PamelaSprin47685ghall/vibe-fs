# log-as-recovery-protocol — Main

## What To Do Now
Remove diagnostic logs from the recovery decision path. Use the durable journal, transaction result, or authoritative external state that actually owns the fact.

## Why This Matters
A log line can precede a crash, fail to flush, change wording, or be emitted twice while the underlying business effect has entirely different semantics. Building recovery on it confuses evidence for operators with commitment for machines.

## Repair Strategy
Identify every recovery question—what was requested, what committed, what external effect is known—and map it to a durable typed source. Keep logs only as supplemental explanation of those facts.

## Wrong Fixes
Do not stabilize log wording and call it a protocol. A string format does not supply atomicity, ordering, idempotency, or durability guarantees.

## Verification
Delete or suppress diagnostic output in a test environment; recovery correctness must remain unchanged. Conversely, a durable fact should be sufficient even if no human-readable log survives.

## Done When
Recovery depends exclusively on channels designed to carry truth, while logs return to their proper role: helping humans understand execution after the fact.
