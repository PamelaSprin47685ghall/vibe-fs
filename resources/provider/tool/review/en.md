# Tool · review

Submit this incumbency's review as a keyed, item-by-item obligation ledger. The review does not advance the stage itself: an empty ledger on the first review wins promotion eligibility; `deliver` performs the actual promotion or completion.

## Contract and Usage

1. **Keyed Findings**: `findings` is an array of `{ key, value }` entries. `key` names one obligation; `value` is the remaining work for that key, or `null` to delete the key. An existing key is overwritten; a missing key is added; a `null` value deletes it. The ledger is updated item by item — never replace the whole set, never accumulate stale duplicates.
2. **First Review Fixes Eligibility**: only the first review for a tenure decides eligibility. An empty ledger on the first review means eligible (the result says eligibility is won); a non-empty ledger means not eligible yet. Later reviews never change eligibility — they only apply keyed updates to the obligation ledger.
3. **Multiple Reviews Allowed**: you may review again to close or adjust individual obligations. Each later review overwrites per key; delete a resolved key by setting its value to `null`.
4. **Deliver Performs Promotion**: when eligible and the ledger is empty, calling `deliver` advances the stage (`S1`→`S2`, `S2`→`S3`, `S3`→delivery authority) or completes the work; its return presents the next stage's method. `deliver` refuses while a resource is still blocked or the ledger is not empty.
5. **Object of Review**: the gap between the current stage and the entry bar for the next stage, not whether the next stage is already finished and not a self-assessed score.
