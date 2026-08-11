# unversioned-schema — Enforcer

## Definition
A durable event, file, wire shape, or cache format changes without an explicit schema version and compatibility rule.

## Trigger When
A durable event, file, wire shape, or cache format changes without an explicit schema version and compatibility rule.

## Do Not Trigger When
Do not fire when the format is ephemeral, never persisted, and never crosses a compatibility boundary.

## Distinguish From
stale-documentation is docs drift; weak-boundary-parsing is untyped ingress; this tip is persistent contracts without versions.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A persistent contract changed without versioning. Add an explicit version and deterministic compatibility policy.

## Examples
### Positive
A durable event, file, wire shape, or cache format changes without an explicit schema version and compatibility rule.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when the format is ephemeral, never persisted, and never crosses a compatibility boundary.
