# truncation-skips-damaged — Main

## What To Do Now
Fail recovery on interior corruption. Permit truncation only when the storage contract proves the damaged bytes are an incomplete final record beyond a verified committed prefix.

## Why This Matters
Later events are interpreted against state produced by earlier events. Skipping a damaged interior segment destroys that state while pretending the later stream remains meaningful, producing a reconstruction with no valid historical derivation.

## Repair Strategy
Use checksums/framing/versioning to distinguish a torn tail from committed records. Stop at the first interior inconsistency and surface repair/restore from authoritative backup rather than guessing past the gap.

## Wrong Fixes
Do not scan for the next plausible record boundary and continue. Syntactic resynchronization cannot recover the semantic state that missing committed history would have produced.

## Verification
Corrupt the tail and an interior record separately. Tail recovery may truncate only under the documented contract; interior corruption must deterministically fail closed.

## Done When
Every replayed record rests on a fully verified committed prefix, and recovery never manufactures continuity across missing history.
