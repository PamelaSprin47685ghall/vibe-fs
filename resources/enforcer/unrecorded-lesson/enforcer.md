# unrecorded-lesson — Enforcer

## Definition
A lesson is unrecorded when debugging, operations, integration, or recovery reveals a reusable fact about the system and that fact disappears with the people or session that discovered it.

## Governing Principle
Experience becomes engineering capital only after it is externalized. An incident may reveal a provider quirk, ordering constraint, failed hypothesis, diagnostic shortcut, or recovery law that source code alone does not make obvious. If that knowledge remains conversational, the organization has learned biologically but not structurally; turnover or context loss resets the system’s effective memory.

## Trigger When
Trigger when an investigation produces a fact or method likely to reduce future search space and no maintained runbook, rule, test, decision record, or project knowledge artifact captures it.

## Do Not Trigger When
Do not trigger when the lesson is genuinely one-off with no plausible reuse, or its substance is already encoded durably in an authoritative artifact.

## Distinguish From
unrecorded-decision preserves rationale for a deliberate choice. repeated-known-mistake fails to reuse existing memory. This rule is the failure to convert newly acquired experience into durable project memory.

## Decision Procedure
Ask whether a future engineer facing a similar symptom would benefit materially from this discovery. If yes, record the smallest durable statement at the artifact type that future work will naturally consult.

## Nudge
A discovery that dies with the session was rented, not learned. Convert reusable experience into tests, runbooks, rules, or records where future work will encounter it before repeating the search.
