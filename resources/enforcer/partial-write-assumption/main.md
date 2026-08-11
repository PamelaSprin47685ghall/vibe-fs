# partial-write-assumption — Main

## What To Do Now
Replace speculative partial-write recovery with the exact outcome model promised by the storage/effect boundary.

## Why This Matters
Recovery code is only as sound as its failure model. Invented intermediate states make the application more complex while weakening correctness because subsequent actions—truncate, retry, compensate—may be based on a state that never actually existed.

## Repair Strategy
Read the owner’s atomicity/durability contract, represent its outcomes explicitly, and handle unknown separately from known failure. Where torn data is genuinely possible, rely on durable markers/checksums that can prove it.

## Wrong Fixes
Do not infer partial commit from timeout length, file size, or “what disks usually do.” Physical intuition is not a substitute for the API’s observable semantics.

## Verification
Fault tests should cover every documented outcome and no branch should require an unobservable invented state.

## Done When
Recovery’s state space is exactly the boundary’s state space: no fewer cases than reality permits, and no extra cases created by fear.
