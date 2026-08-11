# repeated-known-mistake — Main

## What To Do Now
Read the existing lesson or decision before proceeding, verify its premises still hold, and change the current approach accordingly.

## Why This Matters
A documented failure is prepaid reasoning. Ignoring it spends the same debugging cost again and teaches contributors that repository knowledge is ornamental rather than operational. Over time that destroys incentives to record lessons at all.

## Repair Strategy
Link the current symptom to the prior record, apply the recorded constraint, and update the record only if new evidence genuinely changes its scope or conclusion.

## Decision Branches
- If the prior lesson’s premises still hold, follow it and stop repeating the failed approach.
- If premises changed, write an explicit superseding decision with the new evidence, then proceed.
- If no authoritative record exists, fix the defect and capture the lesson rather than treating silence as permission to wander.

## Common Wrong Fixes
- Dismiss prior guidance as “old” without showing which premise changed.
- Copy the lesson into another file instead of keeping one authoritative source and referencing it.
- Add a comment “we know this is bad” and ship the same mechanism anyway.
- Search only chat history and ignore the repository’s recorded decisions.

## Verification
The new solution should avoid the previously identified mechanism, and any changed conclusion should have explicit evidence explaining why the old lesson no longer applies. The invariant is: current action is consistent with still-authoritative recorded constraints, or those constraints are explicitly superseded.

## Done When
Past debugging and design knowledge materially reduces present search space instead of being rediscovered through another failure cycle.
