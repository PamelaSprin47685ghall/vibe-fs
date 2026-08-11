# ephemeral-verification — Main

## What To Do Now
Translate the successful manual probe into the narrowest durable automated check that preserves the discovered invariant.

## Why This Matters
Manual verification proves a moment, not a system. Its setup, commands, and observations disappear, so the next regression begins from ignorance. A repository improves only when debugging knowledge is converted into something that future changes must pass.

## Repair Strategy
Extract the essential stimulus and observable result from the probe. Put them into a unit, contract, replay, integration, or canary test according to the boundary involved. Keep the test deterministic and part of the normal check path.

## Wrong Fixes
Do not save a cryptic scratch script nobody runs, paste terminal output into a comment, or rely on “steps I remember.” Durability requires an executable maintained check, not merely stored evidence.

## Verification
Run the standard project entry point and confirm the new check executes there and fails if the old defect is restored.

## Done When
The knowledge gained during investigation survives the session as a repeatable guard that future work cannot silently bypass.
