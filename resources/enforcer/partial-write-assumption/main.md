# partial-write-assumption — Main

Delete recovery states you cannot observe.

Start from the storage/effect boundary's real contract, not the physical implementation you imagine beneath it. Write down every outcome the caller can actually distinguish and design recovery over exactly that set.

For many boundaries, the honest state space is smaller than engineers expect:

```text
known committed
known not committed
unknown
```

`Unknown` is not a failure of modeling. It is often the most accurate fact available after timeout, process death, or transport loss. Preserve it until an authoritative lookup, idempotency protocol, transaction query, checksum, commit marker, or other boundary-owned evidence resolves it.

If torn/partial data is genuinely possible, model **the evidence that proves it**. Examples:

- length prefix + checksum;
- explicit commit marker;
- WAL/page sequence semantics;
- transaction state exposed by the store;
- provider status endpoint that distinguishes accepted from rejected;
- a durable multipart manifest with per-part commitment.

Do not infer partiality from “a crash happened,” elapsed time, suspicious file size, or low-level folklore when the abstraction above those details promises atomicity.

Common fake repairs:

- truncate the last append after any crash “just in case”;
- rewrite a record whose checksum was never checked;
- add `HalfWritten` to application state even though no API result can ever construct it honestly;
- interpret timeout duration as evidence of how far a write progressed;
- expose underlying filesystem internals through an atomic storage abstraction just so recovery can second-guess it;
- mock impossible outcomes in tests and then treat support for those mocks as resilience;
- collapse `Unknown` into a guessed physical state so the code can take a decisive action.

Verification should be derived from the contract too. Fault-inject every **documented** outcome. Prove recovery is correct for committed, not-committed, unknown, and any explicitly exposed partial/corrupt states.

Then add a meta-test at the abstraction boundary: callers should not reach through the store to inspect implementation residue the contract intentionally hides. If recovery needs that information, either the abstraction is insufficient and must expose a typed fact, or the caller is violating ownership.

Be especially careful with destructive recovery. Truncate/delete/rewrite actions should require positive evidence of corruption or an explicit owner-provided rule. “We are uncertain” is not positive evidence that valid history should be destroyed.

The completed design should have a one-to-one relationship:

```text
observable boundary outcome ↔ recovery branch
```

No missing real state. No invented state. No branch whose precondition is “this probably happened internally.”

> Recovery becomes safer when its imagination gets smaller and its evidence gets stronger.
