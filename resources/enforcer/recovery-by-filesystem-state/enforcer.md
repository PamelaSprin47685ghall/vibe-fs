# recovery-by-filesystem-state — Enforcer

## Definition
Workflow recovery guesses progress from incidental files, directories, temp artifacts, or working-tree shape instead of durable lifecycle facts.

## Trigger When
Recovery infers workflow progress from incidental files, directories, temporary artifacts, or working-tree shape instead of durable lifecycle facts.

## Do Not Trigger When
Do not fire when the filesystem path is the deliberately versioned event/store artifact and recovery reads its schema-backed contents, not directory residue.

## Distinguish From
snapshot-as-truth treats a projection as authority; log-as-recovery-protocol misuses logs; this tip is about inferring lifecycle from FS shape.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Recovery is guessing progress from filesystem residue. Record the lifecycle fact explicitly and recover from it.

## Examples
### Positive
Recovery infers workflow progress from incidental files, directories, temporary artifacts, or working-tree shape instead of durable lifecycle facts.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when the filesystem path is the deliberately versioned event/store artifact and recovery reads its schema-backed contents, not directory residue.
