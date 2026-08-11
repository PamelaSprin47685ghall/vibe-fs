# command-event-confusion — Main

## What To Do Now
Separate the request from the fact it may produce. Validate the command against current state and policy; only after success append an event describing what actually occurred.

## Why This Matters
History must remain replayable under tomorrow’s code. If replay asks today’s authorization rules whether yesterday was allowed, the past changes whenever policy changes. Conversely, storing an unvalidated intention as an event grants history to something that may never have become true.

## Repair Strategy
Give commands and events distinct types, names, and handlers. Commands return typed rejection or emitted events. Event application must be deterministic and policy-free: it reconstructs, it does not renegotiate.

## Decision Branches
- If the record can still be refused, treat it as a command and do not append it as history.
- If the record is a past occurrence, replay it as fact without current policy veto.
- If both meanings currently share one shape, split types before adding flags.

## Common Wrong Fixes
- Do not add an `isValidated` flag to one shared message shape.
- Do not catch replay failures caused by new policy and skip old events.
- Do not re-run authorization during projection as if history were a request.
- Do not persist the command payload as the event “to save a type.”

## Verification
Replay the same event stream under changed current policy; reconstructed historical state must remain identical. Invalid commands must fail before emitting facts. The invariant is that stored events mean “this happened” and commands mean “please attempt this.”

## Done When
Every stored event means “this happened,” every command means “please attempt this,” and no code path needs to guess which meaning a record currently carries.
