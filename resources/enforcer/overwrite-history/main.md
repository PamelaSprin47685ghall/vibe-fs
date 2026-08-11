# overwrite-history — Main

## What To Do Now
Stop editing committed historical facts. Append a correction, compensation, revocation, or superseding event that preserves both what happened and how understanding changed. The append-only historical log (events/journal/audit) is who owns the history invariant that committed facts remain queryable as recorded and corrections appear as new facts.

## Why This Matters
An overwrite gives the present power to forge the past. Even when the new value is "more correct," the system loses when and why the correction occurred, which decisions were made under the earlier fact, and whether replay would reproduce the same historical trajectory.

## Repair Strategy
Keep the event/journal append-only. Derive current views by folding original facts plus corrections. If privacy or legal deletion requirements exist, handle them through an explicitly designed redaction/cryptographic policy rather than casual mutation.

## Decision Branches
- If the record answers "what did we know/do then?", append a compensating fact and leave the original intact.
- If the record is a derived projection, rebuild it from history instead of treating edits there as historical correction.

## Common Wrong Fixes
- Copy the latest truth back into old records to simplify reads.
- Delete the original event and insert a replacement with the same id.
- "Fix" history in a migration that rewrites past rows without a compensating fact.

## Verification
Replay history before and after the correction point. Earlier states must remain historically faithful; later current views must reflect the compensating fact. The invariant is that committed facts stay queryable as they were recorded.

## Done When
The system can answer both "what was recorded then?" and "what is believed now?" without forcing one question to erase the other.
