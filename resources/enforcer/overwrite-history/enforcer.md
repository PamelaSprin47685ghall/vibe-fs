# overwrite-history — Enforcer

History is overwritten when a committed record that answers “what did we know, decide, or do then?” is edited so the present can pretend the corrected version was always true.

Correction and history are different things.

Suppose a charge was recorded as 100, later discovered wrong, and corrected to 80. There are now two facts worth preserving:

1. the system once recorded/acted on 100;
2. later evidence caused that belief to be corrected to 80.

An `UPDATE amount = 80` against the historical row collapses those two facts into one timeless value. The current answer may look cleaner, but the system has destroyed the causal transition that actually happened.

That loss matters whenever someone asks:

- what information was available when an earlier decision was made;
- why a later correction occurred;
- whether replay at time T would reproduce the same behavior;
- which downstream effects were caused by the old belief;
- whether a bug, fraud, migration, or operator action changed history;
- whether audit evidence is complete.

Fire this rule when committed events, journal entries, ledger facts, audit records, decision history, or other “what happened then” records are updated/deleted as ordinary correction.

Do not fire for normal mutable present state. A projection, cache, search index, current balance table, or rebuildable read model may be rewritten freely if immutable/authoritative history exists elsewhere. The rule protects historical testimony, not every row that happens to be durable.

Legal/privacy erasure also needs nuance. GDPR deletion, secret redaction, cryptographic erasure, or court-ordered removal may require content to become unavailable. That is not license for casual silent mutation. A proper policy should still preserve whatever non-sensitive evidence is legally permitted that a redaction/removal occurred, under what authority, and how replay handles it.

Nearby rules:

- `snapshot-as-truth` — a derived projection is promoted over source facts;
- `in-place-mutation` — current shared state is mutated, without necessarily touching history;
- `stale-documentation` — prose no longer matches present behavior;
- `unrecorded-decision` — a consequential choice was never durably captured.

Use this rule when the sharp wound is: **the present has been allowed to forge what the durable past appears to have been.**

The diagnostic question is simple:

> Does this record answer “what is true now?” or “what was recorded then?”

The first may be mutable. The second should normally be append-only, with corrections represented as new facts that supersede, compensate, revoke, or reinterpret earlier ones.

> A fact can become obsolete without becoming un-happened. Correction is itself history.
