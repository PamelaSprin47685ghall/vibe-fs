# unrecorded-lesson — Enforcer

## Definition
A lesson is unrecorded when debugging, operations, integration, or recovery reveals a reusable fact about the system and that fact disappears with the people or session that discovered it.

## Governing Principle
Experience becomes engineering capital only after it is externalized. An incident may reveal a provider quirk, ordering constraint, failed hypothesis, diagnostic shortcut, or recovery law that source code alone does not make obvious. The root-cause is experience remaining conversational: the organization has learned biologically but not structurally, so turnover or context loss resets the system’s effective memory.

## Trigger When
Trigger when an investigation produces a fact or method likely to reduce future search space and no maintained runbook, rule, test, decision record, or project knowledge artifact captures it.

## Do Not Trigger When
- The lesson is genuinely one-off with no plausible reuse.
- Its substance is already encoded durably in an authoritative artifact.
- The finding is a one-time human error with no system constraint to preserve.
- Personal scratch notes that duplicate an existing runbook without adding a new fact.

## Distinguish From
`unrecorded-decision` preserves rationale for a deliberate choice. `repeated-known-mistake` fails to reuse existing memory. Tie-break: if newly acquired experience was not converted into durable project memory, use this rule; if an existing lesson was available and ignored, use `repeated-known-mistake`.

## Decision Procedure
Ask whether a future engineer facing a similar symptom would benefit materially from this discovery. If yes, record the smallest durable statement at the artifact type that future work will naturally consult.

## Examples
- positive: hours spent discovering that a vendor 409 is eventual, then the session ends with no runbook or test.
- near-miss: the same quirk is encoded as a regression test and a one-paragraph runbook entry.
- counterexample: choosing Kafka over Rabbit and never writing why is `unrecorded-decision`.

## Nudge
A discovery that dies with the session was rented, not learned. Convert reusable experience into tests, runbooks, rules, or records where future work will encounter it before repeating the search.
