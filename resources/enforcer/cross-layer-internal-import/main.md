# cross-layer-internal-import — Main

## What To Do Now
Remove the internal import. Either expose the required fact through the provider’s deliberate public contract or relocate the behavior to the layer that already owns the necessary knowledge.

## Why This Matters
Internal imports create obligations no architecture document records. A provider appears free to refactor but is not; a consumer appears dependent only on an interface but actually knows storage layout, helper structure, or lifecycle detail. This is coupling without an honest contract.

## Repair Strategy
Identify why the consumer reached inward. If it needs a stable fact, add the smallest public abstraction that owns that fact. If it needs implementation detail to perform policy, ownership is probably misplaced; move the policy instead.

## Wrong Fixes
Do not re-export the same internal symbol under a public name without defining a stable semantic contract. Renaming leakage is still leakage.

## Verification
Architecture checks should fail on renewed internal imports. The provider’s internal layout should be changeable without touching the consumer.

## Done When
Every cross-layer dependency points at an intentional contract whose owner accepts responsibility for its stability.
