# spike-not-cleaned — Main

## What To Do Now
Rebuild the production path with explicit types, boundaries, error contracts, and tests. Delete spike shortcuts, hard-coded credentials, and throwaway structure.

## Repair Strategy
List every known spike compromise. Either fix each before promote or keep the spike out of the release path. Prefer a clean reimplementation over polishing the experiment in place when structure is wrong.

## Decision Branches
If time forces a phased harden, gate the spike and track each compromise as required work—do not call it done.

## Wrong Fixes
Renaming spike folders and shipping. Adding one test and declaring production-ready. Leaving TODOs on critical paths (see todo-bomb).

## Verification
Production entry points use contracted code; spike artifacts are removed or quarantined; acceptance tests pass on the hardened path.

## Done When
Promoted code meets production contracts; experimental shortcuts are gone from the live path.

## Scope and Authority
Promotion of experiments into production. Not exploratory branches that stay isolated.
