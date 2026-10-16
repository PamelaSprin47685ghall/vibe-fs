# log-as-recovery-protocol — Lesson

## Opinion

### Definition

A diagnostic log becomes a false recovery protocol when restart logic treats **something written for human observation** as though it were the authoritative record of what committed.

The word “log” is overloaded, so classify by semantics, not filename.

A real event journal may also be append-only text or JSON and may literally be called a log. If it has a schema, durability guarantee, ordering model, identity, atomic commit semantics, and replay contract — and the system intentionally writes business facts there — then it is a durable fact store, not the smell this rule describes.

### Governing Principle

The defect is promoting **diagnostic commentary** into machine truth.

Diagnostic channels routinely have weaker guarantees:

- message can be emitted before the effect commits;
- process can crash before buffers flush;
- lines can be sampled, duplicated, reordered, rotated, truncated, redacted, or dropped;
- wording can change for clarity or localization;
- structured logging pipelines can transform fields;
- one business fact can generate several messages, or none;
- retries can emit the same line several times while only one effect commits.

None of those properties are necessarily defects for observability. They are fatal if recovery depends on exact presence/order/wording.

### Trigger When

Fire this rule when:

- restart scans `INFO order committed` to decide which orders exist;
- a daemon recovers the last successful step by grepping its previous stdout/stderr;
- JSON logs are parsed as lifecycle records because “they're structured now”;
- absence of a log line is treated as proof an effect never happened;
- log ordering is treated as causal ordering across async workers;
- operator-visible messages are frozen because recovery parsers depend on them;
- a metrics/tracing event is treated as the durable source of business completion.

### Do Not Trigger When

Do not fire when logs merely explain already-established facts to humans. Do not fire when an observability event is intentionally also the durable event journal **and carries the full commit contract**; in that case name it as the journal and stop treating durability as an incidental property of logging.

### Distinguish From

Nearby rules:

- `stringly-typed-error` — machine control depends on human error prose during normal execution;
- `recovery-by-filesystem-state` — restart infers progress from artifact residue;
- `memory-before-disk` — runtime state advances before durable fact;
- `unrecorded-decision` — a necessary decision has no durable record at all.

### Decision Procedure

A decisive experiment is to suppress diagnostic output entirely while preserving the actual business effects and durable store. If recovery changes, observability has been granted authority it should not have.

The reverse experiment matters too: emit the same diagnostic line without committing the underlying effect. If recovery now believes the effect happened, the log line has become counterfeit testimony.

### Examples


## Advice

### What To Do Now

Move restart authority to a channel designed to carry commitment.

For each recovery question, identify the source that can answer it with the required guarantees:

- what operation was requested;
- whether it committed;
- which logical identity committed;
- what current durable state follows;
- whether an external effect is known success, known failure, or unknown.

That source might be an event journal, transaction table, durable command inbox/outbox, provider status endpoint, authoritative database row, or another typed store. It must own the semantics recovery needs; it cannot merely have printed something nearby.

### Why This Matters

Then demote diagnostic logs back to their proper role: explanation, correlation, debugging, operator context. They may include IDs that help humans locate the authoritative fact, but they must not *become* that fact.

### Repair Strategy

If the current diagnostic channel truly needs to become the recovery store, promote it deliberately: define typed schema, stable identities, commit boundary, durability, ordering, retention, replay, corruption semantics, and migration. At that point it is no longer “just logging”; it is the journal. Rename ownership accordingly so future code does not depend on accidental observability guarantees.

### Decision Branches


### Common Wrong Fixes

Common fake repairs:

- freeze log wording and version it like an API while leaving durability/atomicity undefined;
- switch from plain text to JSON and assume structure implies commitment;
- write both journal and logs, but on restart prefer logs because they are easier to grep;
- emit a “committed” log before transaction commit and rely on convention that nobody crashes in the gap;
- use trace/span completion as proof the underlying effect committed;
- keep logs as a fallback recovery source “just in case the real store is missing,” thereby masking loss/corruption of the actual authority;
- parse stdout from child processes to reconstruct business state when the child has a status/result protocol available.

### Verification

Verification should prove independence from diagnostics:

1. suppress/drop/rotate human logs — recovery remains correct;
2. duplicate/reorder diagnostic messages — recovery remains correct;
3. change prose/localization — recovery remains correct;
4. emit a diagnostic message without committing the business fact — recovery must not believe it;
5. commit a business fact while suppressing the log — recovery must still believe it.

For structured observability, test sampling explicitly. If a tracing backend drops 10% of spans, no business fact should become unrecoverable.

Also inspect retention. Recovery truth cannot live in a channel whose normal operations delete old records on a schedule unrelated to business retention.

### Done When

You are done when every recovery decision cites a typed durable authority, and every log line could disappear without changing machine belief.

> A useful log tells humans what the system believes. A recovery protocol tells the system what it is entitled to believe.

### Nudge

> Diagnostics can describe history. They must not be the only reason history is believed.
