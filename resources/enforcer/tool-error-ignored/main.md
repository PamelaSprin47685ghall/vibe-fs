# tool-error-ignored — Main

## What To Do Now
Resolve the failed tool operation or replace the missing evidence with an explicit alternative before continuing any conclusion that depended on it.

## Why This Matters
A tool failure is not merely an inconvenience; it changes what is known. Ignoring it causes later steps to inherit a premise that was never established, so a polished final result can rest on a silent hole in the evidence chain.

## Repair Strategy
Classify the failure, inspect its cause, rerun only when retry semantics are sound, or use an independent source that proves the same property. Record non-blocking rationale when the failed operation was genuinely irrelevant.

## Wrong Fixes
Do not hide stderr, append `|| true`, or quote later success as proof that the earlier failed check did not matter unless that success actually covers the same property.

## Verification
Every observed error should end in one of two states: resolved, or explicitly superseded by equivalent evidence with a stated reason.

## Done When
No conclusion depends on the imagined success of a tool that actually reported failure.
