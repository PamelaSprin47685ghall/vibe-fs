# command-event-confusion — Main

## What To Do Now
Separate the request from the fact it may produce. Validate the command against current state and policy; only after success append an event describing what actually occurred.

## Why This Matters
History must remain replayable under tomorrow’s code. If replay asks today’s authorization rules whether yesterday was allowed, the past changes whenever policy changes. Conversely, storing an unvalidated intention as an event grants history to something that may never have become true.

## Repair Strategy
Give commands and events distinct types, names, and handlers. Commands return typed rejection or emitted events. Event application must be deterministic and policy-free: it reconstructs, it does not renegotiate.

## Wrong Fixes
Do not add an `isValidated` flag to one shared message shape. Do not catch replay failures caused by new policy and skip old events. Those approaches preserve the category error.

## Verification
Replay the same event stream under changed current policy; reconstructed historical state must remain identical. Invalid commands must fail before emitting facts.

## Done When
Every stored event means “this happened,” every command means “please attempt this,” and no code path needs to guess which meaning a record currently carries.
