# G140-B2: independent feasibility and owner-seam review

Date: 2026-10-19. Reviewer: non-author agent d1_review_git.

Read-only source review of main based on 8b5dd03f7 plus root-owned B1 work. No source edits, builds, tests, dist reads, commits or subprocess product probes were performed. This is a contract review, not implementation acceptance.

## Decision

A minimal correctness prototype is feasible under the existing physical StoreLock. Its lowest correct seam is EventStore.Store.appendOwned, after successful acquireAppendGate and before appendValidated. The same physical ownership interval must cover fresh canonical Current, one synchronous domain decision, exact parents/envelope preparation, physical append, Current commit and lock release. There must be no Host/provider send or awaited domain callback inside this interval.

Production delivery needs a separate explicit freshness/cursor contract first. Calling the current ReloadLocal on every command is correct as a private reference prototype, but violates durable-events-017 if installed as the ordinary atomic append path. The existing source has no canonical incremental freshness cursor that can simply be reused. This is a reason to stage B2, not a reason to add a global domain mutex or another failure ledger.

## Existing owner and call chain

1. ProviderFailureLedger reads ProviderFailureJournalPort.CurrentState, builds FailureRecorded/SuccessRecorded/RetryExhausted outside storage admission, then calls Append. This is the present race.
2. AgentJournalPortAdapter.forProviderFailure translates owner-owned ProviderFailureFactCases to composition AgentFact and JournalAppendFailure to text. Budget/dedupe policy belongs in the provider owner, not in this adapter or Store.
3. AgentJournal.AppendAgent performs ablation denial, calls the writer, then publishes only a committed envelope and its local Journal revision. It reads canonical Current; it does not fold a second projection.
4. EventStoreJournalWriter.Append admits while Open, captures the accepted queue prefix, waits for the previous task, checks prior poison, lazily ensures RuntimeStarted and assigns LocalSeq. commitEnvelope currently chooses TryHeads/AllHeads and dependency-session parents before store.Append.
5. Store.appendOwned acquires the cross-process ProcessEventLog StoreLock, then appendValidated performs identity/parent/payload validation, PrepareLive, synchronous ProcessEventLog append, durability barrier and Current commit. Physical gate release is awaited before the result returns.
6. IntegratorEngine owns all canonical Currents, structural heads, causal parents and semantic faults. Store owns physical append settlement. Journal owns runtime/sequence, lifecycle, poison and publication. Provider owns failure policy. These owners should remain unchanged.

Existing Store NoNewWrite handles empty requests or repeated exact EventId, not atomic domain dedupe. Journal presently invents a new EventId for every append, so that route does not solve this race.

## Required interface propagation

The smallest new capability should be an append-specific synchronous decision, not an arbitrary execute-under-lock API. At EventStore.Port.Contract it deals only in existing EventEnvelope plans, an opaque caller result, and physical append settlement. It must not depend on ProviderFailureProjection, Fact, Journal, Host or canonical engine implementation. The callback runs after canonical freshness is established. Its result distinguishes no new events from a prepared append and carries the domain result without converting expected domain rejection into a storage fault.

The composition writer admits this command through its existing Open/Closing/Poisoned queue. AgentJournal exposes the corresponding composition operation and only publishes actually committed envelopes. The provider adapter supplies a fresh typed ProviderFailureProjection to a pure owner decision. ProviderFailureLedger should extract its existing failure/success transition decisions rather than duplicate them in the adapter. DefaultBudget is still selected only when that fresh decision creates the first real failure.

The new IEventStore method must propagate through WorkspaceEventStore.deferredStore and every real/verification decorator in Surface, EventStoreWriterSurface and JournalPortObservationSurface. IJournalWriter propagation includes EventStoreJournalWriter and ControlledTerminalWriter. Its fsi/owner projects and the bounded contract tests need updating. A fake implementation that falls back to the old prebuilt Append is not a valid implementation of the new contract.

## Freshness is a production prerequisite

IntegratorEngine.ReloadLocal calls ProcessEventLog.readStreams, validates all retained history, k-way merges all events and replays from initialState before replacing state. The decoder prefix cache may avoid unchanged byte reads; it does not avoid whole-history validation, merge, list work or folding. The first append after another writer changes a file may also read that file's new bytes. Existing activated append tests at history lengths 8 and 128 assert no historical event content I/O; the WHAT separately forbids append cost growing with historical event count or bytes.

An incremental decoder is not an incremental canonical Integrator. EventKWayMerge orders ready heads by EventId then WriterId while preserving each writer stream and causal parents. A newly discovered retained stream can insert earlier in the previous canonical order. It is unsafe to append every newly decoded suffix onto the old Current merely because each physical writer is append-only. Retention expiry, imported/replaced writer sets, segmentation changes and causal dependencies also need an explicit canonical freshness rule. Do not infer it from mtime, process-local revision or a watcher notification.

Before production, define which exact durable frontier the Current represents, how an unchanged frontier is proved while holding StoreLock, how new data is integrated without violating canonical ordering, and how unsupported/reordered frontiers fail closed or are explicitly reactivated. Keep this in the canonical owner; do not expose a history reader to the provider. A refresh outside the lock followed by an unchecked decision inside is still a TOCTOU race. Merely renaming a history-dependent append as a command does not satisfy 017.

## Parents, lazy RuntimeStarted and sequence

Parents must be constructed after the fresh decision inside the same physical interval. Moving only the fact callback is insufficient: commitEnvelope currently reads stream heads, all heads for RuntimeStarted and dependency-session heads before store.Append. For multiple planned facts, later facts must see the earlier planned frontier; they cannot all attach to the same stale heads.

NoWrite must neither consume LocalSeq nor publish a JournalChange. If the contract says no new physical write, it also must not force lazy RuntimeStarted. A writer already admitted before Release must drain; a later command is known-not-attempted and must not invoke the decision callback. Prior poison preserves its first physical failure and prevents later domain decisions from becoming permission.

Initialization is a real contract complication, not just a tuple of events. durable-events-006 explicitly says RuntimeStarted failure belongs to initialization and the business fact is still not attempted. Combining init and business into one undifferentiated physical batch would change that failure boundary: after entering a batch append, the business bytes could be partial or complete. A prototype may warm its real writer before measuring concurrency and state that limitation. Production needs explicit initialization settlement and sequence evidence; it must not call store.Append recursively while already holding the same StoreLock. A separately committed init followed by reacquisition must refresh and re-decide before business append, and cannot be described as one callback under one lock without that qualification.

## Settlement and error ownership

- A decision result is tentative until physical settlement and release succeed. Return RetryAuthorized only after that result is safely settled; it is still a budget result, never a send permit.
- Expected domain NoActiveRun, superseded, duplicate or conflict outcomes should not be repackaged as AppendFailed and poison a healthy writer. Carry them as domain results of a no-write decision.
- A NoWrite release failure remains NoNewWriteReleaseFailed. It is not success, not CommitUnknown and not evidence that a new failure fact was written. The original release exception must survive.
- Freshness, decoding, decision or envelope-preparation exceptions before append are known-not-attempted. The acquired gate must still be released and cleanup failures must not replace the primary cause. Wrap the new callback/refresh in the existing preparation/settlement machinery; an exception outside appendValidated currently would otherwise bypass its internal protection.
- After entering physical append, write/barrier/CurrentCommit/release faults retain CommitUnknown and the exact prepared events/cuts. Do not re-run the callback or auto-retry writes to obtain a pleasant answer. Cold inspection is evidence, not a license to resend.
- Refresh may legitimately reveal another writer's already committed facts even if this command later does not write. Compare failure invariants against the fresh pre-command Current, not an intentionally stale snapshot. A foreign refresh does not invent a local Journal revision.
- Semantic cut ownership remains Integrator then Journal fatal, after physical settlement. A domain result must not escape as authorized if its own prepared fact was semantically rejected/cut.

## Finite prototype and delivery plan

1. Freeze a private reference prototype only: the existing StoreLock plus full ReloadLocal, a synchronous pure decide callback, fresh parent selection and existing typed physical settlement. Keep Host sends outside. Warm writers explicitly if lazy initialization is not implemented. Measure and report replay cost; do not install this prototype as production or close B2.
2. Use real canonical Journal, two independent stores/Journals and two OS processes. Both readers start stale at the same barrier. Identical failure identity/reason must produce one physical FailureRecorded and count1. Distinct identities must produce counts1 then2 without semantic cut. Same identity with conflicting reason must leave one fact and a healthy writer. Interleave success and new-root closure to prove expected root/run is read inside admission.
3. Exercise NoWrite release failure, preparation failure, exact before/after-append uncertainty, writer closing/poison, and lazy init separately. Inspect original bytes and independent cold Current. No command result or failure may invoke Host inside the gate.
4. Define and implement canonical freshness as its own bounded work package with valid prefix/frontier, reorder and retention counterexamples plus 017 I/O/cost evidence. Only then migrate the provider decision port and production command consumers.
5. Atomic failure facts alone do not close physical duplicate send. Replayed duplicate failures intentionally can still return RetryAuthorized. The durable prompt claim owner must distinguish newly committed claim from existing Pending/Submitted/Accepted, and only the new claim owner may send. Same-live-Journal B1 cannot prove different-Journal/OS ownership. Test the real consumer and Unknown reentry after that seam is implemented; do not introduce a provider-side duplicate-send set.
6. Fixed production package, affected contract closures, real consumer/cold OS, non-author review, final plan/evidence update. Preserve the old facts2/send2 counterexample and explicitly keep GAP-140 open until these obligations pass.

## Source anchors

- Provider/Attempt/Fallback/Ledger.fs: recordReadyFailure, recordAuthorizedFailure, recordConfirmedSuccess.
- Composition/Durable/AgentJournalPortAdapter.fs: forProviderFailure.
- Persistence/Journal/EventStoreJournalWriter.fs: commitEnvelope, EnsureRuntimeStartedLocked, RunAcceptedAppend, Append.
- Persistence/Journal/AgentJournal.fs: AppendAndPublish, PublishCommitted.
- Persistence/EventStore/Store.fs: appendValidated, appendOwned, settleReleaseFailure.
- Persistence/EventStore/IntegratorEngine.fs: replay, ReloadLocal, PrepareLive.
- Persistence/EventStore/ProcessEventLog.fs: acquireStoreLock, decodeFileCached, readStreams.
- Persistence/EventStore/EventKWayMerge.fs: compareReadyCursor and per-writer cursor progression.
- requirements/durable-events/WHAT.md: 006, 013, 014, 017, 022, 023.
- requirements/durable-events/tests/017.test.mjs and support/append-io-child.mjs.

R12-U1 is not investigated by this review. No evidence here proves ordinary public Host concurrent reachability; the preexisting callable SyncDelegate bypass evidence remains separately classified.
