# debug-print-left — Main

## What To Do Now
Remove temporary prints, dumps, traces, breakpoints, and investigation-only instrumentation from production paths. Promote only signals that have a durable operational purpose.

## Why This Matters
Debug output is optimized for the investigator who already knows the question. Production diagnostics must be optimized for unknown future incidents, stable tooling, privacy, and bounded noise. Treating one as the other leaves an accidental interface that nobody owns.

## Repair Strategy
For each artifact, either delete it or define the operational question it answers, then express that answer through the project’s intentional diagnostic surface with stable fields and level.

## Wrong Fixes
Do not merely lower the log level, rename `console.log`, or leave the artifact behind a rarely used flag. If the signal has no maintained purpose, hiding it is not ownership.

## Verification
Search changed paths for temporary diagnostics and run the relevant flow to confirm production output contains only intentional signals.

## Done When
Every remaining diagnostic has a known consumer or operational question, and no investigation-only artifact survives by inertia.
