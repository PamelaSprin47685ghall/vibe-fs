# overwrite-history — Lesson

## Opinion

### Definition

History is overwritten when a committed record that answers "what did we know, decide, or do then?" is edited so the present can pretend the corrected version was always true.

Correction and history are different things.

### Governing Principle

That loss matters whenever someone asks:

- what information was available when an earlier decision was made;
- why a later correction occurred;
- whether replay at time T would reproduce the same behavior;
- which downstream effects were caused by the old belief;
- whether a bug, fraud, migration, or operator action changed history;
- whether audit evidence is complete.

### Trigger When

Fire this rule when committed events, journal entries, ledger facts, audit records, decision history, or other "what happened then" records are updated/deleted as ordinary correction.

### Do Not Trigger When

Do not fire for normal mutable present state. A projection, cache, search index, current balance table, or rebuildable read model may be rewritten freely if immutable/authoritative history exists elsewhere. The rule protects historical testimony, not every row that happens to be durable.

Legal/privacy erasure also needs nuance. GDPR deletion, secret redaction, cryptographic erasure, or court-ordered removal may require content to become unavailable. That is not license for casual silent mutation. A proper policy should still preserve whatever non-sensitive evidence is legally permitted that a redaction/removal occurred, under what authority, and how replay handles it.

### Distinguish From

Nearby rules:

- `snapshot-as-truth` — a derived projection is promoted over source facts;
- `in-place-mutation` — current shared state is mutated, without necessarily touching history;
- `stale-documentation` — prose no longer matches present behavior;
- `unrecorded-decision` — a consequential choice was never durably captured.

### Decision Procedure

Use this rule when the sharp wound is: **the present has been allowed to forge what the durable past appears to have been.**

The diagnostic question is simple:

> Does this record answer "what is true now?" or "what was recorded then?"

The first may be mutable. The second should normally be append-only, with corrections represented as new facts that supersede, compensate, revoke, or reinterpret earlier ones.

### Examples

Suppose a charge was recorded as 100, later discovered wrong, and corrected to 80. There are now two facts worth preserving:

1. the system once recorded/acted on 100;
2. later evidence caused that belief to be corrected to 80.

An `UPDATE amount = 80` against the historical row collapses those two facts into one timeless value. The current answer may look cleaner, but the system has destroyed the causal transition that actually happened.

## Advice

### What To Do Now

Stop correcting the past by replacing it.

Preserve the original committed fact and append a new fact that states what changed: correction, compensation, revocation, supersession, reclassification, reversal, redaction marker, or another domain-specific transition.

### Why This Matters

The goal is to make two questions answerable at the same time:

- **What was recorded/believed/done at time T?**
- **What is the current interpretation now?**

If one answer requires destroying the other, the history model is too weak.

### Repair Strategy

For event/journal systems, fold current state from original facts plus correcting facts. A correction event should normally identify what it corrects and carry enough reason/provenance to explain the transition without modifying the earlier event.

### Decision Branches

For ledgers/accounting, prefer compensating entries over rewriting balances that were already posted. For audit records, append who/what/why changed the interpretation. For migrations, distinguish "repair malformed storage representation without changing semantic history" from "rewrite what the system claims happened." The latter requires explicit semantic migration policy, not a silent SQL update.

### Common Wrong Fixes

Common fake repairs:

- update old event rows in place and preserve the same event ID;
- delete the original and insert a replacement so replay sees only the corrected story;
- run a migration that normalizes historical values without recording which semantic facts changed;
- copy current truth backward into every old snapshot/record to "make reports consistent";
- mutate history because current-state queries are inconvenient; fix the query/projection instead;
- keep an audit log of the overwrite while the authoritative event itself is still rewritten — the audit log cannot restore causal replay if the source fact changed;
- use "soft delete" flags on historical events without modeling what deletion means to replay.

### Verification

Verification should prove temporal fidelity. Replay/query history just before the correction: it must still expose the old recorded fact. Replay after the correction: current interpretation must reflect the new fact. Both views must be derivable without lying about what happened at either point.

Also test downstream causality. If earlier effects were triggered by the old fact, they should remain explainable. A correction may require compensation, but it must not make those effects appear causeless.

For redaction, test the policy separately: sensitive content becomes unavailable as required, while permitted metadata still demonstrates that a redaction occurred and replay has a deterministic rule for the redacted fact.

### Done When

You are done when the system can tell a truthful story of change:

```text
we believed/recorded X
then evidence/authority Y arrived
therefore X was corrected by Z
current interpretation is W
```

not:

```text
we have always believed W
```

when that second sentence is false.

> Auditability is not keeping old rows forever. It is preserving the causal difference between "was true to the system then" and "is believed now."

### Nudge

> A fact can become obsolete without becoming un-happened. Correction is itself history.
