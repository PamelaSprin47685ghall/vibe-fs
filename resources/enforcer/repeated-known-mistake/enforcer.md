# repeated-known-mistake — Enforcer

## Definition
A known mistake is repeated when current work reenacts a failed approach or violates a constraint that the repository has already recorded as a lesson, invariant, or prior decision.

## Governing Principle
Recording knowledge is valuable only if future decisions can inherit it. Repeating a documented mistake means the project has storage but no retrieval: experience exists as text yet does not constrain action. Engineering maturity therefore requires not merely writing lessons but consulting the knowledge nearest to the problem before paying to rediscover the same failure.

## Trigger When
Trigger when current implementation or investigation follows an approach explicitly recorded as previously failing or forbidden, without a new decision that supersedes the old evidence.

## Do Not Trigger When
- Conditions materially changed and the prior guidance has been explicitly revisited and superseded with a new rationale.
- The recorded lesson applies to a different component, invariant, or failure mechanism than the current work.
- The current path matches the recorded *recommended* approach, not the failed one.
- The lesson is marked historical and no longer claims authority (see stale-documentation for that case).

## Distinguish From
unrecorded-lesson fails to capture new knowledge. stale-documentation contains obsolete guidance. This rule ignores guidance that is still authoritative and relevant. Tie-break: fire here when still-valid recorded knowledge is not consulted; fire unrecorded-lesson when the failure was never written down; fire stale-documentation when the written guidance itself is no longer true.

## Decision Procedure
Locate the prior lesson/decision, compare its premises with current conditions, and either follow it or deliberately supersede it with new evidence. Never silently repeat the old path.

## Examples
- positive: a prior decision forbids sleep-based waits, and the current fix adds `sleep 500` to the same race.
- near-miss: a lesson about SQLite locking is followed while a new Postgres path is designed under different premises and recorded as a superseding decision.
- counterexample: the repository has no prior record of the failure; the defect is new knowledge (that is unrecorded-lesson if it is then left unwritten).

## Nudge
A repository that remembers but does not consult has no practical memory. Reuse recorded failures as constraints unless new evidence explicitly changes the decision.
