# log-as-recovery-protocol — Main

Move restart authority to a channel designed to carry commitment.

For each recovery question, identify the source that can answer it with the required guarantees:

- what operation was requested;
- whether it committed;
- which logical identity committed;
- what current durable state follows;
- whether an external effect is known success, known failure, or unknown.

That source might be an event journal, transaction table, durable command inbox/outbox, provider status endpoint, authoritative database row, or another typed store. It must own the semantics recovery needs; it cannot merely have printed something nearby.

Then demote diagnostic logs back to their proper role: explanation, correlation, debugging, operator context. They may include IDs that help humans locate the authoritative fact, but they must not *become* that fact.

Common fake repairs:

- freeze log wording and version it like an API while leaving durability/atomicity undefined;
- switch from plain text to JSON and assume structure implies commitment;
- write both journal and logs, but on restart prefer logs because they are easier to grep;
- emit a “committed” log before transaction commit and rely on convention that nobody crashes in the gap;
- use trace/span completion as proof the underlying effect committed;
- keep logs as a fallback recovery source “just in case the real store is missing,” thereby masking loss/corruption of the actual authority;
- parse stdout from child processes to reconstruct business state when the child has a status/result protocol available.

If the current diagnostic channel truly needs to become the recovery store, promote it deliberately: define typed schema, stable identities, commit boundary, durability, ordering, retention, replay, corruption semantics, and migration. At that point it is no longer “just logging”; it is the journal. Rename ownership accordingly so future code does not depend on accidental observability guarantees.

Verification should prove independence from diagnostics:

1. suppress/drop/rotate human logs — recovery remains correct;
2. duplicate/reorder diagnostic messages — recovery remains correct;
3. change prose/localization — recovery remains correct;
4. emit a diagnostic message without committing the business fact — recovery must not believe it;
5. commit a business fact while suppressing the log — recovery must still believe it.

For structured observability, test sampling explicitly. If a tracing backend drops 10% of spans, no business fact should become unrecoverable.

Also inspect retention. Recovery truth cannot live in a channel whose normal operations delete old records on a schedule unrelated to business retention.

You are done when every recovery decision cites a typed durable authority, and every log line could disappear without changing machine belief.

> A useful log tells humans what the system believes. A recovery protocol tells the system what it is entitled to believe.