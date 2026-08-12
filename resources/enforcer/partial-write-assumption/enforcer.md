# partial-write-assumption — Enforcer

A partial-write assumption appears when recovery invents a failure state that the storage/effect boundary never promised could exist, then starts making destructive decisions to repair that imagined state.

The mistake is subtle because “disks can fail halfway” sounds prudent. But application recovery does not get to reason from folklore about hardware or from worst-case imagination. It must reason from the **actual contract of the boundary it uses**.

If a database transaction exposes only:

```text
Committed
NotCommitted
Unknown
```

then adding an application state called `HalfCommitted` does not make recovery more robust. It enlarges the state machine with a condition the application cannot observe or prove. Once invented, that condition often justifies dangerous actions: truncate, rewrite, compensate, delete, replay, or “repair” data that may already be perfectly valid.

Fire this rule when:

- timeout is interpreted as “probably half-written” despite an atomic commit contract;
- recovery truncates the final record because file length “looks suspicious,” without a format-level torn-write marker/checksum that actually proves damage;
- code adds states such as `MaybePartial`, `PartiallyPersisted`, `HalfApplied` based on implementation intuition rather than the public durability model;
- an external API's documented outcome is unknown, but application logic invents a finer-grained physical sequence it cannot verify;
- a storage abstraction promises atomic append, while callers still inspect underlying filesystem residue and second-guess whether append was partial;
- tests mock impossible storage outcomes and thereby force production code to support a fantasy failure model.

Do **not** fire when the boundary genuinely exposes partiality. Some formats intentionally permit torn tails and provide durable length prefixes, checksums, sequence numbers, commit markers, or page-level recovery metadata. Some distributed operations really do have multi-step outcomes. If the boundary exposes those facts and recovery can observe them, partiality belongs in the model.

The distinction is evidence, not optimism:

> **Can the application prove this intermediate state from the contract's observable data?**

If yes, model it. If no, do not manufacture it.

Nearby rules:

- `optimistic-retry-assumption` — unknown external effect is reinterpreted as failure and repeated;
- `truncation-skips-damaged` — durable history is actually corrupted and recovery skips/truncates evidence improperly;
- `memory-before-disk` — volatile authority advanced before durable commit;
- `blob-after-event` — reference publication outran referent durability.

A particularly dangerous anti-pattern is “defensive truncation”: on startup, code assumes the last record might be torn and deletes it merely because the previous run crashed. If append was atomic, this can delete the last **valid committed fact** precisely during the incident where history matters most.

The correct discipline is to derive the recovery state space from the boundary owner's semantics. Read the database/storage/provider contract. Encode only states the API can distinguish. Preserve `Unknown` when knowledge is genuinely missing instead of pretending physical imagination is evidence.

> Robust recovery does not mean handling every failure you can imagine. It means handling every failure reality can produce **and you can actually distinguish**.