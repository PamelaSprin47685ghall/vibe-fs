# overwrite-history — Enforcer

## Definition
History is overwritten when previously committed facts are edited or deleted to represent correction instead of preserving the original fact and appending the correcting fact.

## Governing Principle
A fact may later become superseded without ceasing to have happened. Historical systems derive trust from that distinction. Rewriting old records destroys evidence of both the original belief and the later correction, collapsing two events into one timeless value. Audit, replay, causality, and learning all lose the transition that actually occurred.

## Trigger When
Trigger when durable events, journal entries, audit facts, or historical records are mutated/deleted so the past appears as though the corrected state had always been true.

## Do Not Trigger When
Do not trigger for mutable projections/caches explicitly derived from immutable history and rebuildable from it.

## Distinguish From
in-place-mutation changes current shared state. snapshot-as-truth elevates a projection. This rule concerns destruction of committed historical evidence.

## Decision Procedure
Ask whether the record answers “what did we know/do then?” If yes, correction belongs in a new compensating/superseding fact, not an edit to the old one.

## Nudge
Correction is itself history. Preserve the original fact and append the fact that changes its current interpretation; do not erase the path the system actually took.
