# lost-update — Main

## What To Do Now
Install the protocol at the root-cause owner: who owns the record must bind every update to the version it was derived from—versioned compare-and-swap, serialized ownership, or a mathematically valid merge.

## Why This Matters
Without conflict detection, the last writer does not merely win—it can erase information that was already accepted. The system then produces histories in which a successful update effectively never happened, violating the intuitive meaning of commit.

## Repair Strategy
Choose one ownership model. Prefer a single writer when the domain naturally has one authority; otherwise carry version identity from read to atomic commit and return conflict explicitly for recomputation.

## Decision Branches
- If the domain has a natural single authority, serialize writes through that owner.
- If multiple writers must exist, bind each update to its read version via CAS or an explicit merge law.

## Common Wrong Fixes
- Do not add random retries around unconditional writes. Retrying a stale computation can overwrite the newer value again with greater persistence.
- Do not add a lock that is not held across the full read-modify-write.
- Do not last-write-wins by timestamp and call the erased update “resolved.”

## Verification
Run two writers from the same initial version. The system must either serialize them, reject one as stale, or combine them according to an explicit merge law—never silently lose one. That no-silent-erase property is the invariant.

## Done When
Every accepted concurrent update remains represented in the resulting state/history, and stale premises cannot commit unnoticed.
