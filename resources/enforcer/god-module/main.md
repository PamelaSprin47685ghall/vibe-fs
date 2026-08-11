# god-module — Main

## What To Do Now
Split the module along independent invariants, lifecycles, and side-effect boundaries. Keep together only behavior that must change together for one domain reason. Each independent invariant, lifecycle, or side-effect boundary is who owns that sovereignty; the convenience module that merely colocated them is not.

## Why This Matters
A god module destroys locality by making unrelated concepts mutually visible. Its apparent convenience at call sites is paid back as a growing internal state space: every policy can now branch on every resource and every lifecycle phase unless discipline prevents it.

## Repair Strategy
Map responsibilities by reason to change. Extract coherent owners with narrow contracts and move effects behind the boundary that actually owns them. Let an orchestration layer compose results without absorbing their internals.

## Decision Branches
- If two responsibilities share one invariant or lifecycle, keep them together under that owner.
- If they can change independently, extract each sovereignty and leave only composition at the call site.

## Common Wrong Fixes
- Do not split one file into several partial classes/modules while leaving shared mutable state and cross-responsibility imports intact. File count is not architecture.
- Do not extract a utils bucket that still imports every original concern.
- Do not keep the god owner as a singleton that every extracted module must call back into.

## Verification
Each new owner should be explainable and testable without constructing unrelated resources. The invariant of one responsibility must be exercisable without the others; changes in one should not routinely force edits in another.

## Done When
The system has several comprehensible authorities instead of one omniscient module, and each boundary corresponds to a real invariant or lifecycle.
