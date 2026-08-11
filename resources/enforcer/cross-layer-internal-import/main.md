# cross-layer-internal-import — Main

## What To Do Now
Remove the internal import. Either expose the required fact through the provider’s deliberate public contract or relocate the behavior to the layer that already owns the necessary knowledge. The providing layer is who owns internal members; the consumer may depend only on the published contract.

## Why This Matters
Internal imports create obligations no architecture document records. A provider appears free to refactor but is not; a consumer appears dependent only on an interface but actually knows storage layout, helper structure, or lifecycle detail. This is coupling without an honest contract.

## Repair Strategy
Identify why the consumer reached inward. If it needs a stable fact, add the smallest public abstraction that owns that fact. If it needs implementation detail to perform policy, ownership is probably misplaced; move the policy instead.

## Decision Branches
- If the consumer needs a stable fact the provider already owns, publish the smallest public contract for that fact and delete the internal import.
- If the consumer needs implementation detail to enforce policy, move the policy to the owning layer rather than leaking internals outward.
- If the import is same-layer private wiring, leave it; this rule does not police intra-module structure.

## Common Wrong Fixes
- Do not re-export the same internal symbol under a public name without defining a stable semantic contract.
- Do not rename the import path or wrap it in a local alias; renaming leakage is still leakage.
- Do not copy the internal type into the consumer to “avoid importing”; that duplicates the private shape as a second undeclared contract.
- Do not mark the internal module public wholesale so the import becomes legal on paper.

## Verification
Architecture checks should fail on renewed internal imports. The invariant is that the provider’s internal layout can change without touching the consumer.

## Done When
Every cross-layer dependency points at an intentional contract whose owner accepts responsibility for its stability.
