# debug-print-left — Main

## What To Do Now
Remove temporary prints, dumps, traces, breakpoints, and investigation-only instrumentation from production paths. Promote only signals that have a durable operational purpose. Operations is who owns production diagnostics; the original investigator is not who owns leftover prints after the question is settled.

## Why This Matters
Debug output is optimized for the investigator who already knows the question. Production diagnostics must be optimized for unknown future incidents, stable tooling, privacy, and bounded noise. Treating one as the other leaves an accidental interface that nobody owns.

## Repair Strategy
For each artifact, either delete it or define the operational question it answers, then express that answer through the project’s intentional diagnostic surface with stable fields and level.

## Decision Branches
- If no durable consumer exists, delete the artifact from production paths.
- If an operational question remains, rewrite it as a named structured signal with owner, fields, and level.
- If the line is already that intentional surface, leave it; this rule targets leftover investigation machinery.

## Common Wrong Fixes
- Do not merely lower the log level and keep the dump.
- Do not rename `console.log` to a logger call without giving the signal a maintained purpose.
- Do not hide the artifact behind a rarely used flag that still ships.
- Do not comment the print out “in case we need it.”

## Verification
Search changed paths for temporary diagnostics and run the relevant flow to confirm production output contains only intentional signals. The invariant is that every remaining diagnostic has a known consumer or operational question.

## Done When
Every remaining diagnostic has a known consumer or operational question, and no investigation-only artifact survives by inertia.
