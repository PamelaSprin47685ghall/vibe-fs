# unrecorded-lesson — Lesson

## Opinion

### Definition

A lesson is unrecorded when debugging, operations, integration, or recovery reveals a reusable fact about the system and that fact disappears with the people or session that discovered it.

### Governing Principle

Experience becomes engineering capital only after it is externalized. An incident may reveal a provider quirk, ordering constraint, failed hypothesis, diagnostic shortcut, or recovery law that source code alone does not make obvious. The root-cause is experience remaining conversational: the organization has learned biologically but not structurally, so turnover or context loss resets the system’s effective memory.

### Trigger When

Trigger when an investigation produces a fact or method likely to reduce future search space and no maintained runbook, rule, test, decision record, or project knowledge artifact captures it.

### Do Not Trigger When

- The lesson is genuinely one-off with no plausible reuse.
- Its substance is already encoded durably in an authoritative artifact.
- The finding is a one-time human error with no system constraint to preserve.
- Personal scratch notes that duplicate an existing runbook without adding a new fact.

### Distinguish From

`unrecorded-decision` preserves rationale for a deliberate choice. `repeated-known-mistake` fails to reuse existing memory. Tie-break: if newly acquired experience was not converted into durable project memory, use this rule; if an existing lesson was available and ignored, use `repeated-known-mistake`.

### Decision Procedure

Ask whether a future engineer facing a similar symptom would benefit materially from this discovery. If yes, record the smallest durable statement at the artifact type that future work will naturally consult.

### Examples

- positive: hours spent discovering that a vendor 409 is eventual, then the session ends with no runbook or test.
- near-miss: the same quirk is encoded as a regression test and a one-paragraph runbook entry.
- counterexample: choosing Kafka over Rabbit and never writing why is `unrecorded-decision`.

## Advice

### What To Do Now

Capture the reusable discovery in the project artifact future engineers will naturally consult: a regression test, runbook, rule, decision note, contract documentation, or maintained troubleshooting guide. Record it at the affected concept: who owns that concept owns the durable lesson.

### Why This Matters

Debugging cost compounds when discoveries are not retained. The next incident starts with the same false hypotheses, repeated probes, and provider surprises because the system’s durable memory did not improve when its humans did.

### Repair Strategy

Record the causal lesson rather than a diary of commands. Include the symptom, underlying fact, how to verify it, and the action or constraint it implies. Prefer executable preservation where a test or gate can carry the knowledge.

### Decision Branches

If a future engineer with the same symptom would save search space, write the smallest durable artifact they will actually consult.
If the fact is already in a test, runbook, or rule, point to it rather than creating a second copy.

### Common Wrong Fixes

- Dump raw session logs and call the lesson captured.
- Put the finding only in chat or a personal note the project will not search.
- Write a vague “be careful with retries” without the symptom, fact, and verification step.

### Verification

Invariant: a teammate who did not witness the discovery can find the artifact from the affected concept and avoid the known dead end or verify the quirk directly. Durable knowledge must increase.

### Done When

The project’s durable knowledge has increased, so the same class of problem begins from today’s conclusion rather than yesterday’s ignorance.

### Nudge

A discovery that dies with the session was rented, not learned. Convert reusable experience into tests, runbooks, rules, or records where future work will encounter it before repeating the search.
