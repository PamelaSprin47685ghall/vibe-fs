# exception-driven-control-flow — Main

## What To Do Now
Replace expected throw/catch branches with explicit alternatives: option/result types, ordinary branching, iterator protocols, or typed retry outcomes. The function’s return type and local control structure are who own expected alternatives; exception handlers own only failures that invalidate ordinary continuation.

## Why This Matters
Normal control flow should be visible where it is invoked. Exceptions hide possible paths outside the function signature and couple behavior to distant handlers. The result is code whose real branching structure can be understood only by tracking dynamic stack unwinding.

## Repair Strategy
Enumerate the expected outcomes and encode them in the return type or structured control primitive. Keep exceptions for failures that invalidate ordinary continuation, then translate external exception-based APIs once at the adapter boundary if necessary.

## Decision Branches
- If callers routinely handle the outcome, encode it as a local typed alternative, not throw/catch.
- If continuation is impossible (broken invariant, infrastructure collapse), keep an exception.
- If the outcome is a named business refusal, repair under `expected-failure-as-exception`.

## Common Wrong Fixes
- Do not catch a broad exception and convert it into magic nulls or strings.
- Do not wrap every function in try/catch to “make exceptions local.”
- Do not replace throws with a boolean plus a global last-error.
- Do not keep the throw and add a comment that “this is the not-found path.”

## Verification
Callers should be forced by types or explicit syntax to acknowledge every ordinary outcome, and the happy path should no longer depend on exceptions being thrown. The invariant is that expected branching is local and typed.

## Done When
Expected branching is local and typed, while exceptions regain their narrow meaning: the ordinary contract could not be fulfilled in a way normal domain flow can handle.
