# spike-not-cleaned — Main

## What To Do Now
Treat the spike as evidence, not as the final implementation. Preserve what it proved, then replace experimental shortcuts with explicit contracts, owned state, failure semantics, and production verification. Production contracts are who owns shipped structure; the spike owns only the knowledge it discovered.

## Why This Matters
Prototype code is often correct only inside the narrow experiment that gave it meaning. Shipping it unchanged silently extends that local success into claims about concurrency, recovery, security, malformed inputs, and maintenance that were never tested.

## Repair Strategy
Extract the essential idea, discard hard-coded and exploratory structure, then rebuild the smallest production design around the real boundaries and invariants. Delete the spike when its knowledge has transferred.

## Decision Branches
- If the spike is already on the production path, rebuild around contracts and delete experimental shortcuts.
- If the spike is isolated, keep it off the release path or delete it after the lesson is recorded.
- If a wrapper merely calls the spike, replace the wrapper and spike pair with one production implementation.

## Common Wrong Fixes
- Do not keep both “prototype” and “production wrapper” paths where the wrapper merely calls the spike.
- Do not rename the spike module to `prod` without changing assumptions.
- Do not add a few tests of the demo happy path and declare it production-ready.
- Do not leave hard-coded credentials, global state, or skipped failures “until later.”

## Verification
Exercise the production boundary, failure paths, and lifecycle rather than only the successful demo scenario that motivated the spike. The invariant is that shipped structure follows production contracts; the prototype contributes knowledge, not scaffolding.

## Done When
The shipped code owes its structure to production contracts, while the prototype’s only surviving contribution is the knowledge it discovered.
