# log-as-recovery-protocol — Main

## What To Do Now
Move recovery to the root-cause owner: who owns the durable fact (journal, transaction outcome, or authoritative external state) must answer restart. Remove diagnostic logs from that decision path.

## Why This Matters
A log line can precede a crash, fail to flush, change wording, or be emitted twice while the underlying business effect has entirely different semantics. Building recovery on it confuses evidence for operators with commitment for machines.

## Repair Strategy
Identify every recovery question—what was requested, what committed, what external effect is known—and map it to a durable typed source. Keep logs only as supplemental explanation of those facts.

## Decision Branches
- If the channel lacks durability, ordering, and schema guarantees, it cannot drive recovery.
- If the channel is the designed journal, recover from it and keep diagnostics separate.

## Common Wrong Fixes
- Do not stabilize log wording and call it a protocol. A string format does not supply atomicity, ordering, idempotency, or durability guarantees.
- Do not parse structured logs “because they are JSON now.” Format is not a commit.
- Do not dual-write to logs and journal and then prefer the log on restart.

## Verification
Delete or suppress diagnostic output in a test environment; recovery correctness must remain unchanged. Conversely, a durable fact should be sufficient even if no human-readable log survives. The invariant: recovery truth comes only from channels designed to carry commitment.

## Done When
Recovery depends exclusively on channels designed to carry truth, while logs return to their proper role: helping humans understand execution after the fact.
