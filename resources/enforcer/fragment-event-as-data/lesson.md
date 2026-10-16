# fragment-event-as-data — Lesson

## Opinion

### Definition

A transport fragment is mistaken for domain data when the client starts treating **delivery choreography** as though it were the authoritative business fact.

Notifications, patches, partial stream chunks, callbacks, deltas, progress events, websocket frames, and provider-specific updates often answer a narrow question:

> Something changed, or some partial observation became available.

They do not automatically answer:

> What is the complete authoritative state now?

### Governing Principle

That distinction matters because many transport contracts legitimately coalesce, duplicate, reorder, omit intermediate updates, reconnect from a later point, or change patch granularity while still preserving their intended semantics. If domain state is reconstructed by folding such fragments as though every fragment were a durable ordered fact, the client has silently strengthened the transport contract into one the source never promised.

### Trigger When

Fire this rule when:

- websocket/SSE/provider deltas are folded into canonical domain state with no authoritative refresh path;
- reconnect resumes from “now” and the client assumes missing intermediate fragments never mattered;
- duplicate or reordered notifications can create states the source never had;
- a progress/update stream is persisted as business history even though the provider calls it ephemeral;
- callback order is interpreted as state transition order without sequence/commit identity;
- a patch says “field X changed,” but the client applies it to an old base version and treats the result as current truth;
- tests assume one notification per source mutation despite a contract that allows coalescing.

### Do Not Trigger When

Do not fire when the stream **is** the real event source: durable ordered domain facts with stable identity, replay semantics, retention, and an explicit contract that those events are authoritative. Event sourcing is not “fragments as data”; the events are the data by design.

Also do not fire when fragments are used only as wake-up signals: notification arrives, client reads the authoritative snapshot/version, then domain behavior depends on that complete state.

### Distinguish From

Nearby rules:

- `snapshot-as-truth` — a derived projection outranks its source;
- `log-as-recovery-protocol` — diagnostic output becomes durable truth;
- `race-first-wins-semantics` — arrival order chooses business outcome;
- `stale-documentation` is unrelated even if provider docs failed to explain the stream contract.

### Decision Procedure

The decisive question is contract classification:

> Is this channel a **fact log** or a **notification channel**?

If the provider may drop/coalesce/reorder fragments and still consider itself correct, the channel cannot safely define domain history. It can tell you **when to look**, not **what to believe**.

### Examples

A robust client often treats fragments as invalidation hints:

```text
notification arrives
        ↓
identify affected object/version
        ↓
read authoritative state
        ↓
replace/derive local view
```

When complete refresh is expensive, sequence-aware incremental protocols can still be correct — but only if the provider exposes the missing guarantees: base version, event identity, total/causal order as needed, gap detection, replay/resume, and authoritative semantics for each delta.

## Advice

### What To Do Now

Separate notification from truth.

If the transport does not promise durable complete ordered facts, use each fragment only to decide **when/what to refresh**, then obtain the authoritative object/state/version from the source of record.

### Why This Matters

This turns coalescing, duplication, and intermediate omission into latency/efficiency concerns instead of correctness failures.

### Repair Strategy

A common healthy pattern is:

```text
fragment / notification
        ↓
mark object stale or changed
        ↓
fetch/read authoritative version
        ↓
replace local canonical state
        ↓
derive UI/workflow consequences
```

If full snapshot reads are too expensive and incremental state is required, strengthen the protocol rather than adding client heuristics. A safe incremental protocol may need:

- stable event/delta identity;
- source/base version for each patch;
- monotonic sequence or causal relation appropriate to the domain;
- explicit gap detection;
- replay/resume from a known cursor;
- duplicate handling/idempotency;
- snapshot/resync escape hatch when a gap cannot be repaired;
- documented guarantee about whether every semantic transition appears as an event.

### Decision Branches

Without those guarantees, buffering and reordering logic in the client is usually an attempt to manufacture a stronger protocol locally than the provider actually offers.

### Common Wrong Fixes

Common fake repairs:

- debounce harder and hope coalesced updates represent the latest state;
- maintain a large reorder window but no gap-proof source sequence;
- persist received patches as the new system of record;
- on reconnect continue from the newest message without proving what was missed;
- assume duplicate fragments are harmless while applying non-idempotent deltas;
- treat a transport timestamp as authoritative order;
- merge patches onto whatever local base happens to exist;
- add retries for missing fragments when the provider never promised individual fragment replay.

### Verification

Verification should attack the delivery contract deliberately. Drop, duplicate, coalesce, and reorder **non-authoritative** notifications. After refresh/resync, local domain state must converge to the authoritative source.

For a true event protocol, test the opposite: a missing event must be detectable and replay/recovery must restore the exact event history semantics promised by the source. Do not silently fall back to “probably current” behavior.

Also test reconnect and stale-base patches. A delta against version N must never be applied blindly to version N+2 unless the delta contract proves that operation valid.

### Done When

The completion criterion is easy to state:

> Transport behavior may change **when** the client learns that it should refresh. It must not change **which facts** the client ultimately believes, unless the transport itself is the explicitly authoritative fact log.

Once this boundary is clear, UI responsiveness can still use fragments aggressively; business truth remains anchored to the source contract instead of packet choreography.

> Treat ephemeral deltas as hints unless they have earned the stronger title of history.

### Nudge

> Do not promote the shape of transport into the shape of truth. A notification may tell you where to look; only the authoritative contract tells you what happened.
