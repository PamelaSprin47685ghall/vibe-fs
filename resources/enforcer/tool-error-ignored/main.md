# tool-error-ignored — Main

## What To Do Now
Resolve the failed tool operation or replace the missing evidence with an explicit alternative before continuing any conclusion that depended on it. The failed tool’s intended proof obligation is who owns whether later conclusions may proceed; a red signal cannot be inherited as success.

## Why This Matters
A tool failure is not merely an inconvenience; it changes what is known. Ignoring it causes later steps to inherit a premise that was never established, so a polished final result can rest on a silent hole in the evidence chain.

## Repair Strategy
Classify the failure, inspect its cause, rerun only when retry semantics are sound, or use an independent source that proves the same property. Record non-blocking rationale when the failed operation was genuinely irrelevant.

## Decision Branches
If later conclusions depend on the failed operation’s intended result, repair, retry under sound semantics, or replace it with equivalent evidence.
If the failure is irrelevant and another source already proves the same property, record that classification and continue.

## Common Wrong Fixes
- Hide stderr, append `|| true`, or treat a later unrelated success as covering the failed check.
- Retry blindly without understanding whether retry is safe or idempotent.
- Quote a previous green run as if it replaced the failed current observation.

## Verification
Invariant: every observed error ends resolved or explicitly superseded by equivalent evidence with a stated reason. No later conclusion may assume success of a tool that reported failure.

## Done When
No conclusion depends on the imagined success of a tool that actually reported failure.
