# spike-not-cleaned — Main

## What To Do Now
Treat the spike as evidence, not as the final implementation. Preserve what it proved, then replace experimental shortcuts with explicit contracts, owned state, failure semantics, and production verification.

## Why This Matters
Prototype code is often correct only inside the narrow experiment that gave it meaning. Shipping it unchanged silently extends that local success into claims about concurrency, recovery, security, malformed inputs, and maintenance that were never tested.

## Repair Strategy
Extract the essential idea, discard hard-coded and exploratory structure, then rebuild the smallest production design around the real boundaries and invariants. Delete the spike when its knowledge has transferred.

## Wrong Fixes
Do not keep both “prototype” and “production wrapper” paths where the wrapper merely calls the spike. A label does not change the assumptions inside the implementation.

## Verification
Exercise the production boundary, failure paths, and lifecycle rather than only the successful demo scenario that motivated the spike.

## Done When
The shipped code owes its structure to production contracts, while the prototype’s only surviving contribution is the knowledge it discovered.
