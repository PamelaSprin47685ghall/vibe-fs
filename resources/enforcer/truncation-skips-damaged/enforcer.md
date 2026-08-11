# truncation-skips-damaged — Enforcer

## Definition
Recovery skips damage when it encounters corruption inside durable history, discards or bypasses the damaged region, and continues applying later records as though the missing prefix still had a defined meaning. The root-cause is that later records are replayed after their committed prefix has been broken, manufacturing continuity across a causal gap that no longer has a defined starting state.

## Governing Principle
An ordered log gives later facts meaning relative to the prefix before them. Interior corruption removes part of that premise, so subsequent replay no longer has a trustworthy starting state. A final incomplete record is different: if the storage contract permits torn tail writes, truncating only that uncommitted suffix preserves a complete committed prefix. Interior damage breaks the chain of derivation itself.

## Trigger When
Trigger when recovery ignores malformed/checksum-failed/missing records in the middle of durable history and resumes at later entries.

## Do Not Trigger When
- Only the final record is provably incomplete under the storage protocol and recovery truncates precisely that uncommitted tail while preserving the verified prefix.
- Recovery fails closed at the first interior inconsistency and demands restore/repair.
- A replica is discarded before commit because its checksum failed, with no later committed records after the gap.
- Uncommitted speculative buffers are dropped while the committed prefix remains intact.

## Distinguish From
`overwrite-history` deliberately edits past facts. `partial-write-assumption` invents failure states. Tie-break: if recovery proceeds after actual interior historical evidence is untrustworthy, use this rule; if code assumes a partial write pattern that the storage contract does not give, use `partial-write-assumption`.

## Decision Procedure
Locate the first damaged byte/record and determine whether a verified committed record follows it. If yes, fail closed: the later history cannot be interpreted safely without the missing prefix.

## Examples
- positive: checksum fails on record 12, recovery skips to record 13 and continues replay.
- near-miss: the last record is a torn write; recovery truncates only that uncommitted tail.
- counterexample: an operator rewrites earlier events to “fix” history — that is `overwrite-history`.

## Nudge
A log is a causal chain, not a bag of records. Truncate only a provably incomplete tail; interior corruption destroys the premise of every later replay step and must fail closed.
