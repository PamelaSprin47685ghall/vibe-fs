# truncation-skips-damaged — Main

## What To Do Now
Fail recovery on interior corruption. Permit truncation only when the storage contract proves the damaged bytes are an incomplete final record beyond a verified committed prefix.

## Why This Matters
Later events are interpreted against state produced by earlier events. Skipping a damaged interior segment destroys that state while pretending the later stream remains meaningful, producing a reconstruction with no valid historical derivation.

## Repair Strategy
Use checksums/framing/versioning to distinguish a torn tail from committed records. Stop at the first interior inconsistency and surface repair/restore from authoritative backup rather than guessing past the gap.

## Decision Branches
If a verified committed record follows the first damaged byte, fail closed.
If only the uncommitted tail is torn under the storage contract, truncate precisely that suffix.

## Common Wrong Fixes
- Scan for the next plausible record boundary and continue.
- Zero-fill the gap and treat later records as still well-founded.
- Truncate from the damage through the end, discarding verified committed history after a recoverable tail issue.

## Verification
Invariant: every replayed record rests on a fully verified committed prefix. Corrupt the tail and an interior record separately: tail recovery may truncate only under the documented contract; interior corruption must deterministically fail closed.

## Done When
Every replayed record rests on a fully verified committed prefix, and recovery never manufactures continuity across missing history.
