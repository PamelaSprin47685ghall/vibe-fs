# ephemeral-verification — Main

## What To Do Now
Translate the successful manual probe into the narrowest durable automated check that preserves the discovered invariant.

## Why This Matters
Manual verification proves a moment, not a system. Its setup, commands, and observations disappear, so the next regression begins from ignorance. A repository improves only when debugging knowledge is converted into something that future changes must pass.

## Repair Strategy
Extract the essential stimulus and observable result from the probe. Put them into a unit, contract, replay, integration, or canary test according to the boundary involved. Keep the test deterministic and part of the normal check path.

## Decision Branches
- If the probe discovered an invariant the repo must keep, encode that invariant as a maintained check.
- If the session produced no reusable stimulus/result, do not claim verification; obtain durable proof another way.
- If a bug fix lacks any regression, also apply `missing-regression-test`; this rule is specifically about evaporating proof.

## Common Wrong Fixes
- Do not save a cryptic scratch script nobody runs.
- Do not paste terminal output into a comment and call it a test.
- Do not rely on “steps I remember” as the suite.
- Do not add a skipped or manual-only test that CI never executes.

## Verification
Run the standard project entry point and confirm the new check executes there and fails if the old defect is restored. The invariant discovered in the probe must be the property the durable check guards.

## Done When
The knowledge gained during investigation survives the session as a repeatable guard that future work cannot silently bypass.
