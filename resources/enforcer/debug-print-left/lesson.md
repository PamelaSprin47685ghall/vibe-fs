# debug-print-left — Lesson

## Opinion

### Definition

A debug artifact is left behind when temporary output or instrumentation created to answer one local question remains on a production path after that question is settled. The root-cause is that investigation output was never retired or promoted to a diagnostic with an operational owner.

### Governing Principle

Diagnostics are an interface to future operators. Temporary prints have no such contract: their vocabulary, volume, sensitivity, and lifetime were chosen for one investigation. Leaving them in production turns private investigative context into permanent observable behavior without deciding whether anyone should rely on it.

### Trigger When

Trigger when ad hoc `print`, dump, trace, breakpoint, verbose console output, temporary file, or one-off instrumentation remains in shipped code.

### Do Not Trigger When

- The output is an intentional structured diagnostic with a named operational purpose, stable fields, appropriate level, and sensitivity policy.
- The instrumentation is a maintained tracing/metrics surface with an owner, not a leftover print from one debug session.
- A local-only debug flag exists solely in uncommitted or non-shipped tooling and cannot reach production paths.
- Test spies or captured logs are part of an assertion contract and are not emitted as production output.

### Distinguish From

`status-announcement-noise` concerns routine progress chatter even when intentional. `secret-in-code` concerns embedded credentials. This rule is accidental persistence of temporary debugging machinery. Tie-break: if the artifact was created to answer one investigation and has no durable consumer, this rule owns the case even when the line is a log call.

### Decision Procedure

Ask who consumes this diagnostic after the original investigation and what decision it supports. If there is no durable consumer and contract, remove it.

### Examples

- positive: a `console.log` of request bodies remains after the bug hunt, shipping private investigative output.
- near-miss: a structured error log with stable fields, level, and an on-call consumer.
- counterexample: delete the temporary print, or promote the signal onto the project’s intentional diagnostic surface.

## Advice

### What To Do Now

Remove temporary prints, dumps, traces, breakpoints, and investigation-only instrumentation from production paths. Promote only signals that have a durable operational purpose. Operations is who owns production diagnostics; the original investigator is not who owns leftover prints after the question is settled.

### Why This Matters

Debug output is optimized for the investigator who already knows the question. Production diagnostics must be optimized for unknown future incidents, stable tooling, privacy, and bounded noise. Treating one as the other leaves an accidental interface that nobody owns.

### Repair Strategy

For each artifact, either delete it or define the operational question it answers, then express that answer through the project’s intentional diagnostic surface with stable fields and level.

### Decision Branches

- If no durable consumer exists, delete the artifact from production paths.
- If an operational question remains, rewrite it as a named structured signal with owner, fields, and level.
- If the line is already that intentional surface, leave it; this rule targets leftover investigation machinery.

### Common Wrong Fixes

- Do not merely lower the log level and keep the dump.
- Do not rename `console.log` to a logger call without giving the signal a maintained purpose.
- Do not hide the artifact behind a rarely used flag that still ships.
- Do not comment the print out “in case we need it.”

### Verification

Search changed paths for temporary diagnostics and run the relevant flow to confirm production output contains only intentional signals. The invariant is that every remaining diagnostic has a known consumer or operational question.

### Done When

Every remaining diagnostic has a known consumer or operational question, and no investigation-only artifact survives by inertia.

### Nudge

Investigation output is disposable unless promoted deliberately. Remove temporary diagnostics or redesign them as intentional operational signals with a real owner.
