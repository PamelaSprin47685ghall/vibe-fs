# recovery-by-filesystem-state — Main

## What To Do Now
Replace lifecycle inference from file/directory residue with explicit durable events or records whose schema states the recovery fact directly.

## Why This Matters
Incidental artifacts are ambiguous because they can exist before or after the business milestone they seem to imply. Crashes preserve arbitrary prefixes of execution, so existence is not equivalent to completion unless the protocol deliberately makes it so.

## Repair Strategy
Name each recoverable lifecycle fact and persist it at the point of commitment. On restart, read those facts and reconstruct state; use filesystem artifacts only as data referenced by the durable record, not as the record’s substitute.

## Decision Branches
- If a path’s mere existence currently decides a lifecycle step, replace that check with a durable fact written at commitment.
- If the file is the designed store, keep reading its schema-backed contents and stop treating sibling temp names as protocol.
- If artifacts are caches, validate them against the log and discard on mismatch.

## Common Wrong Fixes
- Add more filename conventions, timestamp comparisons, or directory heuristics.
- Encode progress in path prefixes (`done-`, `failed-`) without a schema.
- Keep existence checks and “also” write a log that recovery never reads.
- Treat cleanup of temp files as equivalent to recording completion.

## Verification
Create crash points around artifact creation/cleanup. Recovery must remain correct even when incidental filesystem shape is misleading. The invariant is: lifecycle truth is a durable recorded fact, not the accidental presence of a path.

## Done When
Renaming or reorganizing implementation files cannot silently change workflow recovery semantics because lifecycle truth lives in an explicit durable protocol.
