# partial-write-assumption — Lesson

## Opinion

### Definition

A partial-write assumption appears when recovery invents a failure state that the storage/effect boundary never promised could exist, then starts making destructive decisions to repair that imagined state.

The mistake is subtle because "disks can fail halfway" sounds prudent. But application recovery does not get to reason from folklore about hardware or from worst-case imagination. It must reason from the **actual contract of the boundary it uses**.

If a database transaction exposes only:

```text
Committed
NotCommitted
Unknown
```

then adding an application state called `HalfCommitted` does not make recovery more robust. It enlarges the state machine with a condition the application cannot observe or prove. Once invented, that condition often justifies dangerous actions: truncate, rewrite, compensate, delete, replay, or "repair" data that may already be perfectly valid.

### Governing Principle

The distinction is evidence, not optimism:

> **Can the application prove this intermediate state from the contract's observable data?**

If yes, model it. If no, do not manufacture it.

A particularly dangerous anti-pattern is "defensive truncation": on startup, code assumes the last record might be torn and deletes it merely because the previous run crashed. If append was atomic, this can delete the last **valid committed fact** precisely during the incident where history matters most.

The correct discipline is to derive the recovery state space from the boundary owner's semantics. Read the database/storage/provider contract. Encode only states the API can distinguish. Preserve `Unknown` when knowledge is genuinely missing instead of pretending physical imagination is evidence.

### Trigger When

Fire this rule when:

- timeout is interpreted as "probably half-written" despite an atomic commit contract;
- recovery truncates the final record because file length "looks suspicious," without a format-level torn-write marker/checksum that actually proves damage;
- code adds states such as `MaybePartial`, `PartiallyPersisted`, `HalfApplied` based on implementation intuition rather than the public durability model;
- an external API's documented outcome is unknown, but application logic invents a finer-grained physical sequence it cannot verify;
- a storage abstraction promises atomic append, while callers still inspect underlying filesystem residue and second-guess whether append was partial;
- tests mock impossible storage outcomes and thereby force production code to support a fantasy failure model.

### Do Not Trigger When

Do **not** fire when the boundary genuinely exposes partiality. Some formats intentionally permit torn tails and provide durable length prefixes, checksums, sequence numbers, commit markers, or page-level recovery metadata. Some distributed operations really do have multi-step outcomes. If the boundary exposes those facts and recovery can observe them, partiality belongs in the model.

### Distinguish From

Nearby rules:

- `optimistic-retry-assumption` — unknown external effect is reinterpreted as failure and repeated;
- `truncation-skips-damaged` — durable history is actually corrupted and recovery skips/truncates evidence improperly;
- `memory-before-disk` — volatile authority advanced before durable commit;
- `blob-after-event` — reference publication outran referent durability.

### Decision Procedure

The repair must use the **actual durability success condition** of the boundary. Read the database/storage/provider contract. Encode only states the API can distinguish. Preserve `Unknown` when knowledge is genuinely missing instead of pretending physical imagination is evidence.

### Examples

A database transaction that exposes only:

```text
Committed
NotCommitted
Unknown
```

Adding an application state called `HalfCommitted` does not make recovery more robust. It enlarges the state machine with a condition the application cannot observe or prove. Once invented, that condition often justifies dangerous actions: truncate, rewrite, compensate, delete, replay, or "repair" data that may already be perfectly valid.

## Advice

### What To Do Now

Delete recovery states you cannot observe.

Start from the storage/effect boundary's real contract, not the physical implementation you imagine beneath it. Write down every outcome the caller can actually distinguish and design recovery over exactly that set.

### Why This Matters

For many boundaries, the honest state space is smaller than engineers expect:

```text
known committed
known not committed
unknown
```

`Unknown` is not a failure of modeling. It is often the most accurate fact available after timeout, process death, or transport loss. Preserve it until an authoritative lookup, idempotency protocol, transaction query, checksum, commit marker, or other boundary-owned evidence resolves it.

### Repair Strategy

If torn/partial data is genuinely possible, model **the evidence that proves it**. Examples:

- length prefix + checksum;
- explicit commit marker;
- WAL/page sequence semantics;
- transaction state exposed by the store;
- provider status endpoint that distinguishes accepted from rejected;
- a durable multipart manifest with per-part commitment.

Do not infer partiality from "a crash happened," elapsed time, suspicious file size, or low-level folklore when the abstraction above those details promises atomicity.

### Decision Branches

Be especially careful with destructive recovery. Truncate/delete/rewrite actions should require positive evidence of corruption or an explicit owner-provided rule. "We are uncertain" is not positive evidence that valid history should be destroyed.

### Common Wrong Fixes

Common fake repairs:

- truncate the last append after any crash "just in case";
- rewrite a record whose checksum was never checked;
- add `HalfWritten` to application state even though no API result can ever construct it honestly;
- interpret timeout duration as evidence of how far a write progressed;
- expose underlying filesystem internals through an atomic storage abstraction just so recovery can second-guess it;
- mock impossible outcomes in tests and then treat support for those mocks as resilience;
- collapse `Unknown` into a guessed physical state so the code can take a decisive action.

### Verification

Verification should be derived from the contract too. Fault-inject every **documented** outcome. Prove recovery is correct for committed, not-committed, unknown, and any explicitly exposed partial/corrupt states.

Then add a meta-test at the abstraction boundary: callers should not reach through the store to inspect implementation residue the contract intentionally hides. If recovery needs that information, either the abstraction is insufficient and must expose a typed fact, or the caller is violating ownership.

### Done When

The completed design should have a one-to-one relationship:

```text
observable boundary outcome ↔ recovery branch
```

No missing real state. No invented state. No branch whose precondition is "this probably happened internally."

> Recovery becomes safer when its imagination gets smaller and its evidence gets stronger.

### Nudge

> Robust recovery does not mean handling every failure you can imagine. It means handling every failure reality can produce **and you can actually distinguish**.
