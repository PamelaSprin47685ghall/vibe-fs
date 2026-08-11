# unversioned-schema — Enforcer

## Definition
A schema is unversioned when a durable or cross-version representation can change meaning without carrying an explicit identity for the language in which its bytes were written.

## Governing Principle
Persistence creates communication across time. Old code is a producer and future code is a consumer separated by deployments rather than machines. Without schema version, the consumer must infer which grammar and semantics produced the data, turning compatibility into guesswork. Versioning makes historical interpretation a dispatch on evidence instead of a heuristic on shape.

## Trigger When
Trigger when persisted events, files, wire messages, caches with cross-version lifetime, or other durable contracts evolve without an explicit version and deterministic compatibility/migration rule.

## Do Not Trigger When
Do not trigger for values whose lifetime is strictly within one process/deployment and which never cross a compatibility boundary.

## Distinguish From
guessed-migration is the downstream attempt to recover missing historical semantics. stale-documentation is disagreement in specification. This rule creates the temporal ambiguity by failing to identify the schema itself.

## Decision Procedure
Ask whether bytes produced by an older version may be consumed by a newer one. If yes, assign an explicit schema identity and define how each supported version is read, migrated, or rejected.

## Nudge
Durable bytes are messages to future code. Put the language version in the message and make compatibility a deterministic rule rather than an archaeological inference.
