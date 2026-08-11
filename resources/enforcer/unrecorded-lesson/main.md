# unrecorded-lesson — Main

## What To Do Now
Capture the reusable discovery in the project artifact future engineers will naturally consult: a regression test, runbook, rule, decision note, contract documentation, or maintained troubleshooting guide.

## Why This Matters
Debugging cost compounds when discoveries are not retained. The next incident starts with the same false hypotheses, repeated probes, and provider surprises because the system’s durable memory did not improve when its humans did.

## Repair Strategy
Record the causal lesson rather than a diary of commands. Include the symptom, underlying fact, how to verify it, and the action or constraint it implies. Prefer executable preservation where a test or gate can carry the knowledge.

## Decision Branches
If a future engineer with the same symptom would save search space, write the smallest durable artifact they will actually consult.
If the fact is already in a test, runbook, or rule, point to it rather than creating a second copy.

## Common Wrong Fixes
- Dump raw session logs and call the lesson captured.
- Put the finding only in chat or a personal note the project will not search.
- Write a vague “be careful with retries” without the symptom, fact, and verification step.

## Verification
Invariant: a teammate who did not witness the discovery can find the artifact from the affected concept and avoid the known dead end or verify the quirk directly. Durable knowledge must increase.

## Done When
The project’s durable knowledge has increased, so the same class of problem begins from today’s conclusion rather than yesterday’s ignorance.
