# command-event-confusion — Enforcer

## Definition
Command/event confusion occurs when a request to change the world is stored as though the change already happened, or when a recorded fact is later subjected again to present-day permission or business rules.

## Governing Principle
Intention and fact have opposite epistemic status. A command says “please make this true” and may be refused; an event says “this became true” and may not be vetoed by a later interpretation. Conflating them corrupts either authorization or history: commands gain undeserved certainty, while events become revocable opinions.

## Trigger When
Trigger when desired actions are persisted before validation as completed facts, or replay re-runs current policy to decide whether historical events are still acceptable.

## Do Not Trigger When
- The persisted record is explicitly an intent/request with its own lifecycle, distinct from the event that records eventual completion.
- Command validation happens in the present, and only successful outcomes are appended as events.
- Replay applies events deterministically without consulting current authorization or business policy.
- Read-model projections that skip unknown event types without rewriting history are not re-authorizing the past.

## Distinguish From
`overwrite-history` edits past facts. `guessed-migration` reinterprets old data. This rule is the semantic category error between rejectable intention and irrevocable occurrence. Tie-break: if one record is asked to be both “please” and “it happened,” this rule owns the case.

## Decision Procedure
For each message ask: can the system legitimately say “no” to it now? If yes, it is command-like. Has it already happened and must replay preserve it? If yes, it is event-like. Never assign both roles to one record.

## Examples
- positive: persist `PlaceOrder` as an event before validation, or replay events through today’s permission checks.
- near-miss: a `PlaceOrder` command record stays rejectable until a separate `OrderPlaced` event is appended.
- counterexample: validate the command against current policy; on success append an event that replay applies as fact, policy-free.

## Nudge
Validate intention in the present; record occurrence for the future. Commands may be rejected. Events must be replayed as facts.
