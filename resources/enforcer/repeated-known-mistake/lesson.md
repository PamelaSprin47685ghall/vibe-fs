# repeated-known-mistake — Lesson

## Opinion

### Definition

A known mistake is repeated when current work reenacts a failed approach or violates a constraint that the repository has already recorded as a lesson, invariant, or prior decision. The root-cause is that still-authoritative recorded knowledge is not consulted, so the project pays again for a failure it already documented.

### Governing Principle

Recording knowledge is valuable only if future decisions can inherit it. Repeating a documented mistake means the project has storage but no retrieval: experience exists as text yet does not constrain action. Engineering maturity therefore requires not merely writing lessons but consulting the knowledge nearest to the problem before paying to rediscover the same failure.

### Trigger When

Trigger when current implementation or investigation follows an approach explicitly recorded as previously failing or forbidden, without a new decision that supersedes the old evidence.

### Do Not Trigger When

- Conditions materially changed and the prior guidance has been explicitly revisited and superseded with a new rationale.
- The recorded lesson applies to a different component, invariant, or failure mechanism than the current work.
- The current path matches the recorded *recommended* approach, not the failed one.
- The lesson is marked historical and no longer claims authority (see stale-documentation for that case).

### Distinguish From

unrecorded-lesson fails to capture new knowledge. stale-documentation contains obsolete guidance. This rule ignores guidance that is still authoritative and relevant. Tie-break: fire here when still-valid recorded knowledge is not consulted; fire unrecorded-lesson when the failure was never written down; fire stale-documentation when the written guidance itself is no longer true.

### Decision Procedure

Locate the prior lesson/decision, compare its premises with current conditions, and either follow it or deliberately supersede it with new evidence. Never silently repeat the old path.

### Examples

- positive: a prior decision forbids sleep-based waits, and the current fix adds `sleep 500` to the same race.
- near-miss: a lesson about SQLite locking is followed while a new Postgres path is designed under different premises and recorded as a superseding decision.
- counterexample: the repository has no prior record of the failure; the defect is new knowledge (that is unrecorded-lesson if it is then left unwritten).

## Advice

### What To Do Now

Read the existing lesson or decision before proceeding, verify its premises still hold, and change the current approach accordingly. The still-authoritative recorded lesson or decision is who owns the constraint; current convenience is not who owns permission to repeat the failed path.

### Why This Matters

A documented failure is prepaid reasoning. Ignoring it spends the same debugging cost again and teaches contributors that repository knowledge is ornamental rather than operational. Over time that destroys incentives to record lessons at all.

### Repair Strategy

Link the current symptom to the prior record, apply the recorded constraint, and update the record only if new evidence genuinely changes its scope or conclusion.

### Decision Branches

- If the prior lesson’s premises still hold, follow it and stop repeating the failed approach.
- If premises changed, write an explicit superseding decision with the new evidence, then proceed.
- If no authoritative record exists, fix the defect and capture the lesson rather than treating silence as permission to wander.

### Common Wrong Fixes

- Dismiss prior guidance as “old” without showing which premise changed.
- Copy the lesson into another file instead of keeping one authoritative source and referencing it.
- Add a comment “we know this is bad” and ship the same mechanism anyway.
- Search only chat history and ignore the repository’s recorded decisions.

### Verification

The new solution should avoid the previously identified mechanism, and any changed conclusion should have explicit evidence explaining why the old lesson no longer applies. The invariant is: current action is consistent with still-authoritative recorded constraints, or those constraints are explicitly superseded.

### Done When

Past debugging and design knowledge materially reduces present search space instead of being rediscovered through another failure cycle.

### Nudge

A repository that remembers but does not consult has no practical memory. Reuse recorded failures as constraints unless new evidence explicitly changes the decision.
