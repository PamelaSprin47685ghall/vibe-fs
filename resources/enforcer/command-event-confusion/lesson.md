# command-event-confusion — Lesson

## Opinion

### Definition

Command/event confusion occurs when a request to change the world is stored as though the change already happened, or when a recorded fact is later subjected again to present-day permission or business rules. The root-cause is that one record is asked to be both rejectable intention and irrevocable occurrence, so authorization and history share a shape they cannot both own.

### Governing Principle

Intention and fact have opposite epistemic status. A command says “please make this true” and may be refused; an event says “this became true” and may not be vetoed by a later interpretation. Conflating them corrupts either authorization or history: commands gain undeserved certainty, while events become revocable opinions.

### Trigger When

Trigger when desired actions are persisted before validation as completed facts, or replay re-runs current policy to decide whether historical events are still acceptable.

### Do Not Trigger When

- The persisted record is explicitly an intent/request with its own lifecycle, distinct from the event that records eventual completion.
- Command validation happens in the present, and only successful outcomes are appended as events.
- Replay applies events deterministically without consulting current authorization or business policy.
- Read-model projections that skip unknown event types without rewriting history are not re-authorizing the past.

### Distinguish From

`overwrite-history` edits past facts. `guessed-migration` reinterprets old data. This rule is the semantic category error between rejectable intention and irrevocable occurrence. Tie-break: if one record is asked to be both “please” and “it happened,” this rule owns the case.

### Decision Procedure

For each message ask: can the system legitimately say “no” to it now? If yes, it is command-like. Has it already happened and must replay preserve it? If yes, it is event-like. Never assign both roles to one record.

### Examples

- positive: persist `PlaceOrder` as an event before validation, or replay events through today’s permission checks.
- near-miss: a `PlaceOrder` command record stays rejectable until a separate `OrderPlaced` event is appended.
- counterexample: validate the command against current policy; on success append an event that replay applies as fact, policy-free.

## Advice

### What To Do Now

Separate the request from the fact it may produce. Validate the command against current state and policy; only after success append an event describing what actually occurred. Current policy is who owns whether a command may succeed; the event log is who owns what already happened and must not re-ask permission on replay.

### Why This Matters

History must remain replayable under tomorrow’s code. If replay asks today’s authorization rules whether yesterday was allowed, the past changes whenever policy changes. Conversely, storing an unvalidated intention as an event grants history to something that may never have become true.

### Repair Strategy

Give commands and events distinct types, names, and handlers. Commands return typed rejection or emitted events. Event application must be deterministic and policy-free: it reconstructs, it does not renegotiate.

### Decision Branches

- If the record can still be refused, treat it as a command and do not append it as history.
- If the record is a past occurrence, replay it as fact without current policy veto.
- If both meanings currently share one shape, split types before adding flags.

### Common Wrong Fixes

- Do not add an `isValidated` flag to one shared message shape.
- Do not catch replay failures caused by new policy and skip old events.
- Do not re-run authorization during projection as if history were a request.
- Do not persist the command payload as the event “to save a type.”

### Verification

Replay the same event stream under changed current policy; reconstructed historical state must remain identical. Invalid commands must fail before emitting facts. The invariant is that stored events mean “this happened” and commands mean “please attempt this.”

### Done When

Every stored event means “this happened,” every command means “please attempt this,” and no code path needs to guess which meaning a record currently carries.

### Nudge

Validate intention in the present; record occurrence for the future. Commands may be rejected. Events must be replayed as facts.
