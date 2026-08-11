# recovery-by-filesystem-state — Main

## What To Do Now
Replace lifecycle inference from file/directory residue with explicit durable events or records whose schema states the recovery fact directly.

## Why This Matters
Incidental artifacts are ambiguous because they can exist before or after the business milestone they seem to imply. Crashes preserve arbitrary prefixes of execution, so existence is not equivalent to completion unless the protocol deliberately makes it so.

## Repair Strategy
Name each recoverable lifecycle fact and persist it at the point of commitment. On restart, read those facts and reconstruct state; use filesystem artifacts only as data referenced by the durable record, not as the record’s substitute.

## Wrong Fixes
Do not add more filename conventions, timestamp comparisons, or directory heuristics. Those enlarge the accidental protocol rather than creating authoritative state.

## Verification
Create crash points around artifact creation/cleanup. Recovery must remain correct even when incidental filesystem shape is misleading.

## Done When
Renaming or reorganizing implementation files cannot silently change workflow recovery semantics because lifecycle truth lives in an explicit durable protocol.
