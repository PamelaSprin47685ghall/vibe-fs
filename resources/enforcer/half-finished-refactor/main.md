# half-finished-refactor — Main

## What To Do Now
Choose the post-refactor owner, migrate every repository-owned caller to it, and remove obsolete adapters, aliases, flags, and duplicate implementations.

## Why This Matters
A half migration preserves the cost of the old model while adding the cost of the new one. Every future change must answer which path to update, which representation is canonical, and whether divergence is intentional. The refactor has increased entropy instead of reducing it.

## Repair Strategy
List all old-surface references and eliminate them systematically. Collapse temporary conversion layers once the last legitimate caller moves. Keep compatibility only where an external consumer and retirement plan require it.

## Wrong Fixes
Do not declare the migration “good enough” because both paths work. Dual success is exactly what makes the transitional state capable of surviving forever.

## Verification
Search for retired names and paths, run behavior tests through the new owner, and confirm no internal caller still depends on transitional compatibility.

## Done When
The repository tells one story about ownership again: new structure is authoritative, old structure is gone, and no bridge exists without a bounded external reason.
