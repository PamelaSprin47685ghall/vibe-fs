# race-first-wins-semantics — Main

## What To Do Now
Stop treating completion order as authority. Collect all relevant results (or a defined subset), then merge with an explicit deterministic policy keyed by identity, version, or domain priority.

## Repair Strategy
Introduce a gather/merge step. Prefer associative, commutative merges where possible. Record the merge rule next to the concurrent fan-out.

## Decision Branches
If true first-arrival semantics are required, name and test that policy explicitly. If partial failure is possible, define timeout and incomplete-set handling. If results are identical by construction, document why order is irrelevant.

## Wrong Fixes
Adding sleeps or locks to "stabilize" order. Picking an arbitrary winner without recording why. Silently dropping slower results that carry unique facts.

## Verification
Inject controlled completion orders in tests; the domain result must be identical for every permutation under the merge policy.

## Done When
Domain outcomes no longer depend on scheduler races; merge policy is named, tested, and deterministic.

## Scope and Authority
Concurrent domain aggregation and multi-source reads. Not every fire-and-forget notification.
