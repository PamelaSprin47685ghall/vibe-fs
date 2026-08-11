# exception-driven-control-flow — Main

## What To Do Now
Replace expected throw/catch branches with explicit alternatives: option/result types, ordinary branching, iterator protocols, or typed retry outcomes.

## Why This Matters
Normal control flow should be visible where it is invoked. Exceptions hide possible paths outside the function signature and couple behavior to distant handlers. The result is code whose real branching structure can be understood only by tracking dynamic stack unwinding.

## Repair Strategy
Enumerate the expected outcomes and encode them in the return type or structured control primitive. Keep exceptions for failures that invalidate ordinary continuation, then translate external exception-based APIs once at the adapter boundary if necessary.

## Wrong Fixes
Do not catch a broad exception and convert it into magic nulls or strings. That merely trades one hidden channel for another.

## Verification
Callers should be forced by types or explicit syntax to acknowledge every ordinary outcome, and the happy path should no longer depend on exceptions being thrown.

## Done When
Expected branching is local and typed, while exceptions regain their narrow meaning: the ordinary contract could not be fulfilled in a way normal domain flow can handle.
