# fragment-event-as-data — Main

Separate notification from truth.

If the transport does not promise durable complete ordered facts, use each fragment only to decide **when/what to refresh**, then obtain the authoritative object/state/version from the source of record.

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

This turns coalescing, duplication, and intermediate omission into latency/efficiency concerns instead of correctness failures.

If full snapshot reads are too expensive and incremental state is required, strengthen the protocol rather than adding client heuristics. A safe incremental protocol may need:

- stable event/delta identity;
- source/base version for each patch;
- monotonic sequence or causal relation appropriate to the domain;
- explicit gap detection;
- replay/resume from a known cursor;
- duplicate handling/idempotency;
- snapshot/resync escape hatch when a gap cannot be repaired;
- documented guarantee about whether every semantic transition appears as an event.

Without those guarantees, buffering and reordering logic in the client is usually an attempt to manufacture a stronger protocol locally than the provider actually offers.

Common fake repairs:

- debounce harder and hope coalesced updates represent the latest state;
- maintain a large reorder window but no gap-proof source sequence;
- persist received patches as the new system of record;
- on reconnect continue from the newest message without proving what was missed;
- assume duplicate fragments are harmless while applying non-idempotent deltas;
- treat a transport timestamp as authoritative order;
- merge patches onto whatever local base happens to exist;
- add retries for missing fragments when the provider never promised individual fragment replay.

Verification should attack the delivery contract deliberately. Drop, duplicate, coalesce, and reorder **non-authoritative** notifications. After refresh/resync, local domain state must converge to the authoritative source.

For a true event protocol, test the opposite: a missing event must be detectable and replay/recovery must restore the exact event history semantics promised by the source. Do not silently fall back to “probably current” behavior.

Also test reconnect and stale-base patches. A delta against version N must never be applied blindly to version N+2 unless the delta contract proves that operation valid.

The completion criterion is easy to state:

> Transport behavior may change **when** the client learns that it should refresh. It must not change **which facts** the client ultimately believes, unless the transport itself is the explicitly authoritative fact log.

Once this boundary is clear, UI responsiveness can still use fragments aggressively; business truth remains anchored to the source contract instead of packet choreography.

> Treat ephemeral deltas as hints unless they have earned the stronger title of history.