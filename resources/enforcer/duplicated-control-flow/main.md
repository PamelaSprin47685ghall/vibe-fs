# duplicated-control-flow — Main

## What To Do Now
Move the genuinely shared workflow or transition protocol to one owner and make every caller invoke that canonical behavior.

## Why This Matters
Copied control flow duplicates time-sensitive knowledge: ordering, short-circuit rules, retry boundaries, and cleanup. Drift rarely appears as a dramatic fork; one branch gets a new condition, another misses it, and the system quietly acquires multiple definitions of the same process.

## Repair Strategy
Prove first that the sequences represent the same knowledge. Then extract the smallest operation that owns their common protocol, parameterizing only true variation rather than exposing the whole algorithm as callbacks.

## Wrong Fixes
Do not abstract merely because lines look alike. Do not create a generic orchestration framework whose parameters are more complex than the duplicated sequence. Shared knowledge deserves one owner; coincidental shape does not.

## Verification
A future change to ordering or failure semantics should have one authoritative edit point, with callers covered by behavior tests.

## Done When
One protocol has one implementation owner, and no caller can silently evolve a private version of the same workflow.
