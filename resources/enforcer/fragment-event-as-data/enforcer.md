# fragment-event-as-data — Enforcer

A transport fragment is mistaken for domain data when the client starts treating **delivery choreography** as though it were the authoritative business fact.

Notifications, patches, partial stream chunks, callbacks, deltas, progress events, websocket frames, and provider-specific updates often answer a narrow question:

> Something changed, or some partial observation became available.

They do not automatically answer:

> What is the complete authoritative state now?

That distinction matters because many transport contracts legitimately coalesce, duplicate, reorder, omit intermediate updates, reconnect from a later point, or change patch granularity while still preserving their intended semantics. If domain state is reconstructed by folding such fragments as though every fragment were a durable ordered fact, the client has silently strengthened the transport contract into one the source never promised.

Fire this rule when:

- websocket/SSE/provider deltas are folded into canonical domain state with no authoritative refresh path;
- reconnect resumes from “now” and the client assumes missing intermediate fragments never mattered;
- duplicate or reordered notifications can create states the source never had;
- a progress/update stream is persisted as business history even though the provider calls it ephemeral;
- callback order is interpreted as state transition order without sequence/commit identity;
- a patch says “field X changed,” but the client applies it to an old base version and treats the result as current truth;
- tests assume one notification per source mutation despite a contract that allows coalescing.

Do not fire when the stream **is** the real event source: durable ordered domain facts with stable identity, replay semantics, retention, and an explicit contract that those events are authoritative. Event sourcing is not “fragments as data”; the events are the data by design.

Also do not fire when fragments are used only as wake-up signals: notification arrives, client reads the authoritative snapshot/version, then domain behavior depends on that complete state.

Nearby rules:

- `snapshot-as-truth` — a derived projection outranks its source;
- `log-as-recovery-protocol` — diagnostic output becomes durable truth;
- `race-first-wins-semantics` — arrival order chooses business outcome;
- `stale-documentation` is unrelated even if provider docs failed to explain the stream contract.

The decisive question is contract classification:

> Is this channel a **fact log** or a **notification channel**?

If the provider may drop/coalesce/reorder fragments and still consider itself correct, the channel cannot safely define domain history. It can tell you **when to look**, not **what to believe**.

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

> Do not promote the shape of transport into the shape of truth. A notification may tell you where to look; only the authoritative contract tells you what happened.