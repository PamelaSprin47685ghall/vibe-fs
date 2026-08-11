# debug-print-left — Enforcer

## Definition
A debug artifact is left behind when temporary output or instrumentation created to answer one local question remains on a production path after that question is settled.

## Governing Principle
Diagnostics are an interface to future operators. Temporary prints have no such contract: their vocabulary, volume, sensitivity, and lifetime were chosen for one investigation. Leaving them in production turns private investigative context into permanent observable behavior without deciding whether anyone should rely on it.

## Trigger When
Trigger when ad hoc `print`, dump, trace, breakpoint, verbose console output, temporary file, or one-off instrumentation remains in shipped code.

## Do Not Trigger When
- The output is an intentional structured diagnostic with a named operational purpose, stable fields, appropriate level, and sensitivity policy.
- The instrumentation is a maintained tracing/metrics surface with an owner, not a leftover print from one debug session.
- A local-only debug flag exists solely in uncommitted or non-shipped tooling and cannot reach production paths.
- Test spies or captured logs are part of an assertion contract and are not emitted as production output.

## Distinguish From
`status-announcement-noise` concerns routine progress chatter even when intentional. `secret-in-code` concerns embedded credentials. This rule is accidental persistence of temporary debugging machinery. Tie-break: if the artifact was created to answer one investigation and has no durable consumer, this rule owns the case even when the line is a log call.

## Decision Procedure
Ask who consumes this diagnostic after the original investigation and what decision it supports. If there is no durable consumer and contract, remove it.

## Examples
- positive: a `console.log` of request bodies remains after the bug hunt, shipping private investigative output.
- near-miss: a structured error log with stable fields, level, and an on-call consumer.
- counterexample: delete the temporary print, or promote the signal onto the project’s intentional diagnostic surface.

## Nudge
Investigation output is disposable unless promoted deliberately. Remove temporary diagnostics or redesign them as intentional operational signals with a real owner.
