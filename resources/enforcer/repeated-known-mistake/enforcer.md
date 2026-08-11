# repeated-known-mistake — Enforcer

## Definition
A known mistake is repeated when current work reenacts a failed approach or violates a constraint that the repository has already recorded as a lesson, invariant, or prior decision.

## Governing Principle
Recording knowledge is valuable only if future decisions can inherit it. Repeating a documented mistake means the project has storage but no retrieval: experience exists as text yet does not constrain action. Engineering maturity therefore requires not merely writing lessons but consulting the knowledge nearest to the problem before paying to rediscover the same failure.

## Trigger When
Trigger when current implementation or investigation follows an approach explicitly recorded as previously failing or forbidden, without a new decision that supersedes the old evidence.

## Do Not Trigger When
Do not trigger when conditions materially changed and the prior guidance has been explicitly revisited and superseded with a new rationale.

## Distinguish From
unrecorded-lesson fails to capture new knowledge. stale-documentation contains obsolete guidance. This rule ignores guidance that is still authoritative and relevant.

## Decision Procedure
Locate the prior lesson/decision, compare its premises with current conditions, and either follow it or deliberately supersede it with new evidence. Never silently repeat the old path.

## Nudge
A repository that remembers but does not consult has no practical memory. Reuse recorded failures as constraints unless new evidence explicitly changes the decision.
