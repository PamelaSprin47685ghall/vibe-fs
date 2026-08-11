# partial-write-assumption — Main

## What To Do Now
Replace speculative partial-write recovery with the exact outcome model promised by the storage/effect boundary. The storage/effect boundary that defines atomicity is who owns the recovery invariant that the handled outcome set equals the contract's observable outcomes.

## Why This Matters
Recovery code is only as sound as its failure model. Invented intermediate states make the application more complex while weakening correctness because subsequent actions—truncate, retry, compensate—may be based on a state that never actually existed.

## Repair Strategy
Read the owner's atomicity/durability contract, represent its outcomes explicitly, and handle unknown separately from known failure. Where torn data is genuinely possible, rely on durable markers/checksums that can prove it.

## Decision Branches
- If the contract is atomic or only `{committed, not committed, unknown}`, recover over those outcomes and drop imagined half-writes.
- If the contract exposes torn records with durable evidence, detect that evidence and recover from it—do not invent extra states beyond it.

## Common Wrong Fixes
- Infer partial commit from timeout length, file size, or "what disks usually do."
- Truncate or rewrite on suspicion, destroying valid committed data.
- Fold unknown into "must be partial" so recovery becomes destructive by default.

## Verification
Fault tests should cover every documented outcome and no branch should require an unobservable invented state. The invariant is that recovery's state space equals the boundary's observable state space.

## Done When
Recovery's state space is exactly the boundary's state space: no fewer cases than reality permits, and no extra cases created by fear.
