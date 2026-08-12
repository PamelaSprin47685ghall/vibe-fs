# log-as-recovery-protocol — Enforcer

A diagnostic log becomes a false recovery protocol when restart logic treats **something written for human observation** as though it were the authoritative record of what committed.

The word “log” is overloaded, so classify by semantics, not filename.

A real event journal may also be append-only text or JSON and may literally be called a log. If it has a schema, durability guarantee, ordering model, identity, atomic commit semantics, and replay contract — and the system intentionally writes business facts there — then it is a durable fact store, not the smell this rule describes.

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

Fire this rule when:

- restart scans `INFO order committed` to decide which orders exist;
- a daemon recovers the last successful step by grepping its previous stdout/stderr;
- JSON logs are parsed as lifecycle records because “they're structured now”;
- absence of a log line is treated as proof an effect never happened;
- log ordering is treated as causal ordering across async workers;
- operator-visible messages are frozen because recovery parsers depend on them;
- a metrics/tracing event is treated as the durable source of business completion.

Do not fire when logs merely explain already-established facts to humans. Do not fire when an observability event is intentionally also the durable event journal **and carries the full commit contract**; in that case name it as the journal and stop treating durability as an incidental property of logging.

Nearby rules:

- `stringly-typed-error` — machine control depends on human error prose during normal execution;
- `recovery-by-filesystem-state` — restart infers progress from artifact residue;
- `memory-before-disk` — runtime state advances before durable fact;
- `unrecorded-decision` — a necessary decision has no durable record at all.

A decisive experiment is to suppress diagnostic output entirely while preserving the actual business effects and durable store. If recovery changes, observability has been granted authority it should not have.

The reverse experiment matters too: emit the same diagnostic line without committing the underlying effect. If recovery now believes the effect happened, the log line has become counterfeit testimony.

> Diagnostics can describe history. They must not be the only reason history is believed.