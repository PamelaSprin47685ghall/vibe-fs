# wholesale-rewrite — Main

## What To Do Now
Reduce the change to the smallest structurally correct transformation that satisfies the new contract while preserving known-good code and verified behavior outside that boundary.

## Why This Matters
A rewrite expands uncertainty faster than it expands value. Every replaced line reopens assumptions that previous production history may already have settled, so the verification burden grows from “prove this new behavior” toward “re-prove the subsystem.”

## Repair Strategy
Map the required semantic delta, retain unaffected owners and paths, migrate only the structures whose invariants truly changed, and use tests to protect preserved behavior during the transformation.

## Wrong Fixes
Do not equate fewer old lines with cleaner architecture. Deletion is valuable only when it removes obsolete knowledge, not when it discards working knowledge that still belongs in the new design.

## Verification
The diff should be explainable as a bounded semantic transformation, and unchanged behavior should remain covered without recreating it from scratch.

## Done When
The solution changes exactly the assumptions the requirement invalidated and leaves the rest of the system’s accumulated evidence intact.
