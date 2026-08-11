# duplicated-control-flow — Enforcer

Id: enforcement-b08 / Family: B / Ordinal: 18

## ScoreWhen

The same workflow, retry sequence, validation order, or state transition algorithm is independently implemented in multiple places.

## Nudge

The same control algorithm has multiple owners. Establish one canonical implementation and route all callers through it.
