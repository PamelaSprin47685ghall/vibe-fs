# command-event-confusion — Enforcer

## Definition
Command/event confusion occurs when a request to change the world is stored as though the change already happened, or when a recorded fact is later subjected again to present-day permission or business rules.

## Governing Principle
Intention and fact have opposite epistemic status. A command says “please make this true” and may be refused; an event says “this became true” and may not be vetoed by a later interpretation. Conflating them corrupts either authorization or history: commands gain undeserved certainty, while events become revocable opinions.

## Trigger When
Trigger when desired actions are persisted before validation as completed facts, or replay re-runs current policy to decide whether historical events are still acceptable.

## Do Not Trigger When
Do not trigger when the persisted record is explicitly an intent/request with its own lifecycle, distinct from the event that records eventual completion.

## Distinguish From
overwrite-history edits past facts. guessed-migration reinterprets old data. This rule is the semantic category error between rejectable intention and irrevocable occurrence.

## Decision Procedure
For each message ask: can the system legitimately say “no” to it now? If yes, it is command-like. Has it already happened and must replay preserve it? If yes, it is event-like. Never assign both roles to one record.

## Nudge
Validate intention in the present; record occurrence for the future. Commands may be rejected. Events must be replayed as facts.
