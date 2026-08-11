# unversioned-schema — Enforcer

## Definition
A schema is unversioned when a durable or cross-version representation can change meaning without carrying an explicit identity for the language in which its bytes were written.

## Governing Principle
Persistence creates communication across time. Old code is a producer and future code is a consumer separated by deployments rather than machines. The root-cause is durable bytes without language identity. Without schema version, the consumer must infer which grammar and semantics produced the data, turning compatibility into guesswork. Versioning makes historical interpretation a dispatch on evidence instead of a heuristic on shape.

## Trigger When
Trigger when persisted events, files, wire messages, caches with cross-version lifetime, or other durable contracts evolve without an explicit version and deterministic compatibility/migration rule.

## Do Not Trigger When
- Values whose lifetime is strictly within one process/deployment and which never cross a compatibility boundary.
- In-memory DTOs that are never persisted, cached across deploys, or sent to older/newer peers.
- Contracts that already carry an explicit schema identity plus read/migrate/reject rules.
- Ephemeral debug dumps not used as a compatibility surface.

## Distinguish From
`guessed-migration` is the downstream attempt to recover missing historical semantics. `stale-documentation` is disagreement in specification. Tie-break: if the temporal ambiguity comes from failing to identify the schema itself, use this rule; if code infers historical meaning from shape because version is missing, that repair still starts here rather than as `guessed-migration` alone.

## Decision Procedure
Ask whether bytes produced by an older version may be consumed by a newer one. If yes, assign an explicit schema identity and define how each supported version is read, migrated, or rejected.

## Examples
- positive: an event log adds a field and changes meaning with no version byte; readers infer from field presence.
- near-miss: each event carries `schemaVersion` and unknown versions fail with a typed compatibility error.
- counterexample: docs still describe the old event shape while code writes the new one — that is `stale-documentation`.

## Nudge
Durable bytes are messages to future code. Put the language version in the message and make compatibility a deterministic rule rather than an archaeological inference.
