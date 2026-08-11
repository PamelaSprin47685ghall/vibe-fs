# status-announcement-noise — Main

## What To Do Now
Remove or aggregate routine status chatter and retain communication only where it changes the recipient’s understanding, decision, or required action. The emitting channel is who owns the attention invariant that each remaining message must change the recipient’s model.

## Why This Matters
A communication channel has finite human bandwidth. Low-information updates dilute high-information events and train recipients to skim, precisely the behavior that makes a later failure or decision easy to miss.

## Repair Strategy
Define meaningful phase boundaries, report concise evidence at those boundaries, and prefer final results over narration of every internal action. Use structured telemetry for machine consumption rather than conversational noise.

## Decision Branches
If the message changes a decision, fact, failure, uncertainty, or required action, keep it.
If it only narrates routine motion, remove or aggregate it to the next real phase boundary.

## Common Wrong Fixes
- Shorten every status line while keeping the same frequency and empty semantics.
- Mute the entire channel, including genuine failures and decisions.
- Relocate the same chatter into a dashboard that still emits low-information events.

## Verification
Invariant: each remaining status message must change the recipient’s model. Confirm every leftover update answers what changed, what was decided, what failed, what remains uncertain, or what the receiver must do.

## Done When
The channel is sparse enough that a new message deserves attention and dense enough in information that reading it updates the recipient’s model of the work.
