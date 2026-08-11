# overwrite-history — Enforcer

## Definition
History is overwritten when previously committed facts are edited or deleted to represent correction instead of preserving the original fact and appending the correcting fact. The root-cause is that correction is implemented as mutation of a committed fact, destroying the original event and the later change as distinct history.

## Governing Principle
A fact may later become superseded without ceasing to have happened. Historical systems derive trust from that distinction. Rewriting old records destroys evidence of both the original belief and the later correction, collapsing two events into one timeless value. Audit, replay, causality, and learning all lose the transition that actually occurred.

## Trigger When
Trigger when durable events, journal entries, audit facts, or historical records are mutated/deleted so the past appears as though the corrected state had always been true.

## Do Not Trigger When
- The target is a mutable projection or cache explicitly derived from immutable history and rebuildable from it.
- The write updates current operational state that was never claimed as historical fact.
- Redaction follows an explicit legal/cryptographic policy that still records that a fact was removed.

## Distinguish From
in-place-mutation changes current shared state. snapshot-as-truth elevates a projection. Tie-break: if committed historical evidence is destroyed, this rule; if current mutable state is updated in place, in-place-mutation; if a projection is treated as the source of truth, snapshot-as-truth.

## Decision Procedure
Ask whether the record answers "what did we know/do then?" If yes, correction belongs in a new compensating/superseding fact, not an edit to the old one.

## Examples
- positive: A billing event's amount is UPDATEd after a dispute so reports look as if the original charge never happened.
- near-miss: A materialized balance table is rebuilt from the event log after a compensating event.
- counterexample: The ledger appends `ChargeCorrected` and leaves the original `Charged` intact.

## Nudge
Correction is itself history. Preserve the original fact and append the fact that changes its current interpretation; do not erase the path the system actually took.
