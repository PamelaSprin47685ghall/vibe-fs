# MF04 convergence: defer package wiring

Decision: keep the current main implementation for this round. This is a read-only source comparison, not a measured performance result or an end-to-end convergence proof.

## Actual production path

- `IntegratorEngine.fs:292` calls `EventKWayMerge.mergeRetained` during replay.
- Both production replay entry points (`IntegratorEngine.fs:392` emergency full replay and `:534` normal load) obtain streams from `ProcessEventLog.readStreams`.
- `ProcessEventLog.fs:1002` groups all physical forms by WriterId; `writerPhysicalMetadata` and `readStreamsAt` preserve one complete logical stream for each unique writer. This aggregation occurs before the merge call.
- The other direct calls are `MergeSurface.fs:121/124`, a JS requirement proof surface, rather than a separate production reader.

## Semantic comparison with fixed upstream

Current main `EventKWayMerge.fs` and fixed library `202a41ab:src/convergence/KWayMerge.fs` both use a ready min-heap, per-parent waiter tokens, duplicate waiters, writer cursor offsets, causal readiness before EventId ordering, and events-owned identity collision checking. Retained mode treats parents absent from the entire retained set as outside the window, while in-set causal cycles remain invalid. The package does not add persistence, crash, lock, or business-fold guarantees.

There is one real API difference: current main `mergeCore` first uses `Map.ofList` and silently takes the later stream under a repeated writer name; the package rejects repeated writer names as NonCanonical. This is useful fail-closed behavior at a general raw-stream API. No production path found above can currently produce repeated names, because the reader has already grouped them. It therefore does not establish a user-facing need for the package bridge in this round. Keep it as a narrow defensive follow-up if a new caller or concrete bad input reaches the typed merge directly; a local repeated-writer guard would also close that boundary without migrating algorithms.

## Cost boundary

Fixed main `5880b824:EventKWayMerge.fs` converts every typed envelope through `CanonicalEventCodec.toLibraryEvent`, remembers DTO-to-envelope references in a new Map, converts writer/event lists to JS arrays, and maps the result back to the original envelopes and an F# list. The library then allocates per-writer Ids and Parents arrays on readStream. A normal main merge without duplicate event IDs calls CanonicalEventCodec only when it encounters an identity duplicate; it does not convert every event through the codec before merging.

These allocations are visible in the actual production functions, but no time or heap numbers were measured. Both algorithms have the same heap/waiter shape; the existing main implementation is not a repeated global-sort algorithm awaiting an asymptotic upgrade. A raw JS package benchmark would omit the typed adapter cost and would not answer the acceptance question.

## Smallest honest measurement if needed later

Compile two isolated narrow Fable projects from the unchanged production Identity/Model/StoreTypes/CanonicalEventCodec/EventKWayMerge sources. Use current main for one and the fixed 5880 typed codec/bridge plus fixed 202a package for the other. Construct actual typed EventEnvelope lists once outside the timed section, and invoke the actual typed mergeRetained entry, including its bridge. Use bounded independent and cross-writer parent chains at two small history sizes; verify equal ordered IDs, rejection categories, returned envelope reference identity, and diagnostic comparisons before comparing time. Keep dependency versions and Node/Bun runtimes fixed. Report cold construction and merge time separately, with no claim about disk IO, business Current, cross-process convergence, or GAP151.

This narrow measurement was not started because the routing integration window took priority. No main source, library source, metadata, or generated main artifact was changed or compiled for this decision.
