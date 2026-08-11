# debug-print-left — Enforcer

## Definition
A debug artifact is left behind when temporary output or instrumentation created to answer one local question remains on a production path after that question is settled.

## Governing Principle
Diagnostics are an interface to future operators. Temporary prints have no such contract: their vocabulary, volume, sensitivity, and lifetime were chosen for one investigation. Leaving them in production turns private investigative context into permanent observable behavior without deciding whether anyone should rely on it.

## Trigger When
Trigger when ad hoc `print`, dump, trace, breakpoint, verbose console output, temporary file, or one-off instrumentation remains in shipped code.

## Do Not Trigger When
Do not trigger for intentional structured diagnostics with a named operational purpose, stable fields, appropriate level, and sensitivity policy.

## Distinguish From
status-announcement-noise concerns routine progress chatter even when intentional. secret-in-code concerns embedded credentials. This rule is accidental persistence of temporary debugging machinery.

## Decision Procedure
Ask who consumes this diagnostic after the original investigation and what decision it supports. If there is no durable consumer and contract, remove it.

## Nudge
Investigation output is disposable unless promoted deliberately. Remove temporary diagnostics or redesign them as intentional operational signals with a real owner.
