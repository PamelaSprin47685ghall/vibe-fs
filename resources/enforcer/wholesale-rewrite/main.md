# wholesale-rewrite — Main

## What To Do Now
Reduce the change to the smallest structurally correct transformation that satisfies the new contract while preserving known-good code and verified behavior outside that boundary. Limit blast radius: who owns the invalidated invariants owns the rewrite surface.

## Why This Matters
A rewrite expands uncertainty faster than it expands value. Every replaced line reopens assumptions that previous production history may already have settled, so the verification burden grows from “prove this new behavior” toward “re-prove the subsystem.”

## Repair Strategy
Map the required semantic delta, retain unaffected owners and paths, migrate only the structures whose invariants truly changed, and use tests to protect preserved behavior during the transformation.

## Decision Branches
If the requirement invalidates only a small set of invariants, preserve the rest and transform that set.
If a recorded decision says the structure itself is the defect, rewrite that structure and still keep unrelated proven code.

## Common Wrong Fixes
- Equate fewer old lines with cleaner architecture.
- Copy behavior into a new package while leaving the old one half-alive.
- Rewrite tests from scratch so preserved behavior is no longer pinned.

## Verification
Invariant: the diff is a bounded semantic transformation of invalidated assumptions only. Unchanged behavior should remain covered without recreating it from scratch.

## Done When
The solution changes exactly the assumptions the requirement invalidated and leaves the rest of the system’s accumulated evidence intact.
