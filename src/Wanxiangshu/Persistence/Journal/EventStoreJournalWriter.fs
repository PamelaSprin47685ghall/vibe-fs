namespace Wanxiangshu.Persistence.Journal

open System
open System.Text
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Host
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Composition.Durable

/// Local payload writer for EventStore journals.
/// `BlobRef` keeps the long-standing `blobs/<handle>` application shape, while
/// `<handle>` is now the local content-addressed PayloadRef. Git OIDs do not
/// participate in runtime durability.
type EventStoreBlobWriter private (store: IEventStore) =
    member _.Write(content: string) : Task<Result<BlobWriteReceipt, string>> =
        task {
            try
                let digest = BlobDigest.create (HostDigest.sha256Hex content)
                let bytes = Encoding.UTF8.GetBytes content

                match! store.WritePayload bytes with
                | Error error -> return Error error
                | Ok payloadRef ->
                    return
                        Ok
                            { BlobRef = BlobRef.create ("blobs/" + PayloadRef.value payloadRef)
                              BlobDigest = digest }
            with ex ->
                return Error(sprintf "event-store payload write failed: %s" ex.Message)
        }

    member _.Read(blobRef: BlobRef) : Task<Result<string, string>> =
        task {
            let relative = BlobRef.value blobRef
            let prefix = "blobs/"

            if not (relative.StartsWith(prefix, StringComparison.Ordinal)) then
                return Error(sprintf "invalid blob reference: %s" relative)
            else
                let handle = relative.Substring(prefix.Length)

                if String.IsNullOrWhiteSpace handle || handle.Contains "/" then
                    return Error(sprintf "invalid blob reference: %s" relative)
                else
                    match! store.ReadPayload(PayloadRef.create handle) with
                    | Error error -> return Error error
                    | Ok None -> return Error(sprintf "event-store payload missing: %s" handle)
                    | Ok(Some bytes) ->
                        try
                            return Ok(Encoding.UTF8.GetString bytes)
                        with ex ->
                            return Error(sprintf "event-store payload read failed: %s" ex.Message)
        }

    interface IBlobWriter with
        member this.Write(content) = this.Write content
        member this.Read(blobRef) = this.Read blobRef

    static member Create(store: IEventStore) : IBlobWriter =
        EventStoreBlobWriter(store) :> IBlobWriter

/// Unified payload closure for one Journal fact (DURABLE-EVENTS-012): every
/// EventStore payload the fact references. This is the single mapping that makes
/// `EventEnvelope.PayloadRefs` authoritative instead of always empty. Journal
/// facts carry blob handles inline in the domain payload, so the closure is
/// derived at the append boundary rather than tracked twice.
[<RequireQualifiedAccess>]
module JournalPayloadClosure =

    let ofFact (fact: Fact) : PayloadRef list =
        let refs =
            match fact with
            | Fact.Runtime _ -> []
            | Fact.MagicTodo payload ->
                match MagicTodoFactCodec.tryDecode payload with
                | Ok magic -> MagicTodoFactCodec.payloadRefs magic
                | Error _ -> []
            | Fact.Agent agent ->
                match agent with
                | AgentFact.Execution execution ->
                    match execution with
                    | ExecutionFactCases.HandleCompleted p ->
                        (p.CompletionRef |> Option.toList |> List.map MagicTodoFactCodec.payloadRefOfBlobRef)
                        @ (p.CompletionDigest |> Option.toList |> List.map MagicTodoFactCodec.payloadRefOfBlobDigest)
                    | ExecutionFactCases.HandleFalseCompletionRejected p ->
                        [ MagicTodoFactCodec.payloadRefOfBlobRef p.ExpectedCompletionRef
                          MagicTodoFactCodec.payloadRefOfBlobDigest p.ExpectedCompletionDigest ]
                    | ExecutionFactCases.HandleFalseTerminalReported p ->
                        [ MagicTodoFactCodec.payloadRefOfBlobRef p.BadCompletionRef
                          MagicTodoFactCodec.payloadRefOfBlobDigest p.BadCompletionDigest ]
                    | ExecutionFactCases.ParentJoinCorrectionRequested p ->
                        [ MagicTodoFactCodec.payloadRefOfBlobDigest p.BadCompletionDigest ]
                    | _ -> []
                | AgentFact.Companion companion ->
                    match companion with
                    | CompanionFactCases.XTracePartAppended p ->
                        [ MagicTodoFactCodec.payloadRefOfBlobRef p.TextRef
                          MagicTodoFactCodec.payloadRefOfBlobDigest p.TextDigest ]
                    | CompanionFactCases.TerminalOutputCaptured p ->
                        [ MagicTodoFactCodec.payloadRefOfBlobRef p.TextRef
                          MagicTodoFactCodec.payloadRefOfBlobDigest p.TextDigest ]
                    | _ -> []
                | AgentFact.Context context ->
                    match context with
                    | ContextFactCases.BlogObservationCommitted p ->
                        [ MagicTodoFactCodec.payloadRefOfBlobRef p.TextRef
                          MagicTodoFactCodec.payloadRefOfBlobDigest p.TextDigest ]
                        @ (p.EvidenceRef |> Option.toList |> List.map MagicTodoFactCodec.payloadRefOfBlobRef)
                    | ContextFactCases.BlogObservationsSquashed p ->
                        [ MagicTodoFactCodec.payloadRefOfBlobRef p.TextRef
                          MagicTodoFactCodec.payloadRefOfBlobDigest p.TextDigest ]
                    | ContextFactCases.BloggerRequestMaterialized p ->
                        [ MagicTodoFactCodec.payloadRefOfBlobRef p.ContextRef
                          MagicTodoFactCodec.payloadRefOfBlobDigest p.ContextDigest ]
                        @ (p.SelectedFrameDigests |> List.map MagicTodoFactCodec.payloadRefOfBlobDigest)
                    | ContextFactCases.PrefixRebaseCommitted p ->
                        [ MagicTodoFactCodec.payloadRefOfBlobRef p.FrozenRecordPrefixRef
                          MagicTodoFactCodec.payloadRefOfBlobDigest p.FrozenRecordPrefixDigest ]
                    | _ -> []
                | AgentFact.Fission fission ->
                    match fission with
                    | FissionFactCases.FissionAdmitted p ->
                        [ MagicTodoFactCodec.payloadRefOfBlobRef p.OwnerWorkRecordRef
                          MagicTodoFactCodec.payloadRefOfBlobDigest p.OwnerWorkRecordDigest ]
                    | FissionFactCases.FissionLaneMaterialized p ->
                        [ MagicTodoFactCodec.payloadRefOfBlobRef p.WorkRecordRef
                          MagicTodoFactCodec.payloadRefOfBlobDigest p.WorkRecordDigest ]
                    | FissionFactCases.FissionCompletionCaptured p ->
                        [ MagicTodoFactCodec.payloadRefOfBlobRef p.PayloadRef
                          MagicTodoFactCodec.payloadRefOfBlobDigest p.PayloadDigest ]
                    | FissionFactCases.FissionConverged p ->
                        [ MagicTodoFactCodec.payloadRefOfBlobRef p.AggregateWorkRecordRef
                          MagicTodoFactCodec.payloadRefOfBlobDigest p.AggregateWorkRecordDigest ]
                    | _ -> []
                | _ -> []
            | Fact.ManagerLifecycle lifecycle ->
                match lifecycle with
                | ManagerLifecycleFact.LifeOpened p ->
                    [ MagicTodoFactCodec.payloadRefOfBlobRef p.OpeningTextRef
                      MagicTodoFactCodec.payloadRefOfBlobDigest p.OpeningTextDigest ]
                | ManagerLifecycleFact.FinalityRequested p ->
                    [ MagicTodoFactCodec.payloadRefOfBlobRef p.LastWordsRef
                      MagicTodoFactCodec.payloadRefOfBlobDigest p.LastWordsDigest ]
                | ManagerLifecycleFact.FinalityRejected p ->
                    [ MagicTodoFactCodec.payloadRefOfBlobRef p.WorkRecordRef
                      MagicTodoFactCodec.payloadRefOfBlobDigest p.WorkRecordDigest ]
                | ManagerLifecycleFact.FinalitySiblingSteered p ->
                    [ MagicTodoFactCodec.payloadRefOfBlobRef p.WorkRecordRef
                      MagicTodoFactCodec.payloadRefOfBlobDigest p.WorkRecordDigest ]
                | ManagerLifecycleFact.FinalityBlessed p ->
                    [ MagicTodoFactCodec.payloadRefOfBlobRef p.WorkRecordBundleRef
                      MagicTodoFactCodec.payloadRefOfBlobDigest p.WorkRecordBundleDigest ]
                | ManagerLifecycleFact.LifeCompleted p ->
                    [ MagicTodoFactCodec.payloadRefOfBlobRef p.TerminalRef
                      MagicTodoFactCodec.payloadRefOfBlobDigest p.TerminalDigest ]
                | _ -> []

        PayloadRefs.canonicalize refs

/// Journal writer backed by the local process EventStore.
/// It never enumerates history and never folds facts itself. CanonicalIntegrator
/// owns both boot replay and live integration; this type only assigns journal
/// envelope identity/sequence and appends one universal EventEnvelope.
type EventStoreJournalWriter
    private (runtimeId: RuntimeId, blobWriter: IBlobWriter, store: IEventStore, initialCurrentSeq: int64) =
    let gate = obj ()
    // DSL-MUTABLE: resource — next LocalSeq for this fresh RuntimeId only.
    let mutable currentSeq = initialCurrentSeq
    // DSL-MUTABLE: resource — local append poison latch.
    let mutable poisoned = false
    // DSL-MUTABLE: resource — dispose latch.
    let mutable disposed = false
    // DSL-MUTABLE: resource — serialized process writer operations.
    let mutable serial = Task.FromResult(())

    member _.RuntimeId = runtimeId
    member _.BlobWriter = blobWriter
    member _.FilePath = ""
    member _.LocalSeq = lock gate (fun () -> currentSeq)
    member _.LastCommittedLocalSeq = lock gate (fun () -> currentSeq - 1L)
    member _.IsPoisoned = lock gate (fun () -> poisoned)
    member _.TryCurrent(key: string) = store.TryCurrent key

    static member private formatAppendError(error: AppendError) : string =
        match error with
        | AppendError.StorageInvalid detail -> sprintf "storage invalid: %A" detail
        | AppendError.AppendFailed reason -> "append failed: " + reason

    static member private commitEnvelope (store: IEventStore) (envelope: Envelope) : Task<Result<unit, AppendError>> =
        let streamId = EventStoreJournalCodec.encodeStreamId envelope.Stream

        let parents =
            match envelope.Fact with
            | Fact.Runtime(RuntimeStarted _) ->
                let heads = store.AllHeads()
                if List.isEmpty heads then [] else heads
            | _ -> store.TryHead streamId |> Option.toList

        let encoded = EventStoreJournalCodec.encode parents (JournalPayloadClosure.ofFact envelope.Fact) envelope
        store.Append [ encoded ]

    static member private currentJournalProjection(store: IEventStore) : Result<ProjectionSet, FoldRejection> =
        match store.TryCurrent "Journal" with
        | Some current -> Ok(unbox<ProjectionSet> current)
        | None -> Ok Fold.empty

    static member private initEnvelope (runtimeId: RuntimeId) (processId: int) (startedAt: DateTimeOffset) : Envelope =
        { RuntimeId = runtimeId
          LocalSeq = LocalSeq.create 1L
          ObservedAt = startedAt
          EventId = EventId.create (Guid.NewGuid().ToString("N"))
          Stream = StreamId.Workspace
          ProviderRun = None
          Fact =
            Fact.Runtime(
                RuntimeStarted
                    {| RuntimeId = runtimeId
                       ProcessId = processId
                       StartedAt = startedAt |}
            ) }

    /// Fresh runtime: every process receives a fresh RuntimeId and therefore
    /// LocalSeq starts at 1. Prior history is already in CanonicalIntegrator.Current.
    static member create
        (runtimeId: RuntimeId, processId: int, startedAt: DateTimeOffset, store: IEventStore)
        : Task<IJournalWriter * Envelope> =
        task {
            let init = EventStoreJournalWriter.initEnvelope runtimeId processId startedAt

            match! EventStoreJournalWriter.commitEnvelope store init with
            | Error error ->
                return
                    failwith (
                        sprintf
                            "EventStore journal create failed to append RuntimeStarted: %s"
                            (EventStoreJournalWriter.formatAppendError error)
                    )
            | Ok() ->
                let writer =
                    EventStoreJournalWriter(runtimeId, EventStoreBlobWriter.Create store, store, 2L)

                return writer :> IJournalWriter, init
        }

    /// Boot never reads history here. WorkspaceEventStore already replayed every
    /// process writer stream through CanonicalIntegrator before this call.
    static member resumeOrCreate
        (runtimeId: RuntimeId, processId: int, startedAt: DateTimeOffset, store: IEventStore)
        : Task<Result<IJournalWriter * Envelope * ProjectionSet, FoldRejection>> =
        task {
            let init = EventStoreJournalWriter.initEnvelope runtimeId processId startedAt

            match! EventStoreJournalWriter.commitEnvelope store init with
            | Error error ->
                return
                    failwith (
                        sprintf
                            "EventStore journal resumeOrCreate failed to append RuntimeStarted: %s"
                            (EventStoreJournalWriter.formatAppendError error)
                    )
            | Ok() ->
                match EventStoreJournalWriter.currentJournalProjection store with
                | Error rejection -> return Error rejection
                | Ok projection ->
                    let writer =
                        EventStoreJournalWriter(runtimeId, EventStoreBlobWriter.Create store, store, 2L)

                    return Ok(writer :> IJournalWriter, init, projection)
        }

    member private _.AppendLocked
        (stream: StreamId)
        (providerRun: ProviderRunIdentity option)
        (fact: Fact)
        : Task<CommitResult<Envelope>> =
        task {
            let eventId = EventId.create (Guid.NewGuid().ToString("N"))

            if poisoned || disposed then
                return CommitUnknown(eventId, WriteFailed "Writer is poisoned or disposed")
            else
                let envelope: Envelope =
                    { RuntimeId = runtimeId
                      LocalSeq = LocalSeq.create currentSeq
                      ObservedAt = DateTimeOffset.UtcNow
                      EventId = eventId
                      Stream = stream
                      ProviderRun = providerRun
                      Fact = fact }

                match! EventStoreJournalWriter.commitEnvelope store envelope with
                | Ok() ->
                    currentSeq <- currentSeq + 1L
                    return Committed envelope
                | Error error ->
                    poisoned <- true
                    return CommitUnknown(eventId, WriteFailed(EventStoreJournalWriter.formatAppendError error))
        }

    member private _.Enqueue(work: unit -> Task<'T>) : Task<'T> =
        lock gate (fun () ->
            let previous = serial

            let running =
                task {
                    do! previous
                    return! work ()
                }

            serial <-
                task {
                    let! _ = running
                    return ()
                }

            running)

    member this.Append
        (stream: StreamId)
        (providerRun: ProviderRunIdentity option)
        (fact: Fact)
        : Task<CommitResult<Envelope>> =
        this.Enqueue(fun () -> this.AppendLocked stream providerRun fact)

    member _.Release() = lock gate (fun () -> disposed <- true)

    member this.ReleaseAsync() =
        this.Release()
        ValueTask()

    interface IJournalWriter with
        member this.RuntimeId = this.RuntimeId
        member this.BlobWriter = this.BlobWriter
        member this.FilePath = this.FilePath
        member this.LocalSeq = this.LocalSeq
        member this.LastCommittedLocalSeq = this.LastCommittedLocalSeq
        member this.IsPoisoned = this.IsPoisoned
        member this.TryCurrent(key) = this.TryCurrent key
        member this.Append stream providerRun fact = this.Append stream providerRun fact
        member this.Release() = this.Release()
        member this.ReleaseAsync() = this.ReleaseAsync()
