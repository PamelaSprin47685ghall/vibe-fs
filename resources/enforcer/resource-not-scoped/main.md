# resource-not-scoped — Main

## What To Do Now
Wrap acquisition and disposal in one scope (bracket, use, defer, try/finally, owning type). Ensure failure paths dispose. Prefer ownership types over manual close calls scattered across functions.

## Repair Strategy
Identify every resource open site. Attach a single owner. Add cleanup on cancel and exception. For subprocesses and worktrees, register teardown with the parent session.

## Decision Branches
If lifetime spans requests, make the session object the owner and document end conditions. If a pool is required, bound it and define eviction.

## Wrong Fixes
Opening handles in helpers and hoping callers close them. Catch blocks that return without dispose. Leaking worktrees after agent exit.

## Verification
Fault-inject mid-lifetime; resources are released. Process/fd counts return to baseline after the scope ends.

## Done When
Every acquired resource has a clear owner and deterministic disposal on all exits.

## Scope and Authority
I/O and process resources. Not pure value lifetimes.
