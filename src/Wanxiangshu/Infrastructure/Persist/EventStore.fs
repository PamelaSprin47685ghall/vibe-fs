namespace Wanxiangshu.Infrastructure.Persist

open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity

/// Application-facing event store port (§2.4 / §9 / §10).
type IEventStore =
    abstract OpenSnapshot: unit -> Task<StoreSnapshot>

    abstract Append:
        baseSnapshot: StoreSnapshot * events: EventEnvelope list -> Task<Result<StoreSnapshot, AppendError>>

    abstract Refresh: unit -> Task<StoreSnapshot>
    abstract Merge: snapshots: StoreSnapshot list -> Task<Result<StoreSnapshot, MergeError>>
    abstract Publish: candidate: AppendCandidate -> Task<Result<StoreSnapshot, PublishError>>
    abstract Converge: remote: string -> Task<Result<StoreSnapshot, ConvergeError>>

/// Test double: forwards reads/writes; CompareAndSwapRef always rejects.
type CasRejectGitRawStore(inner: IGitRawStore) =
    interface IGitRawStore with
        member _.WriteBlob(content) = inner.WriteBlob content
        member _.WriteTree(entries) = inner.WriteTree entries
        member _.ReadObject(oid) = inner.ReadObject oid
        member _.ReadTree(oid) = inner.ReadTree oid
        member _.ReadRef(refName) = inner.ReadRef refName
        member _.CompareAndSwapRef(_, _, _) = Task.FromResult false

[<RequireQualifiedAccess>]
module EventStore =
    [<Literal>]
    let DefaultMaxRetries = 8

    let private emptySnapshot (store: IGitRawStore) : Task<StoreSnapshot> =
        task {
            match! GitRawStore.materializeSnapshot store [] with
            | Ok snapshot -> return snapshot
            | Error err -> return failwith (sprintf "empty store materialization failed: %A" err)
        }

    let private readPublished (store: IGitRawStore) : Task<StoreSnapshot option> =
        task {
            let! oid = store.ReadRef StoreRef.canonical
            return oid |> Option.map (fun value -> { RootOid = RootOid.create value })
        }

    let openSnapshot (store: IGitRawStore) : Task<StoreSnapshot> =
        task {
            match! readPublished store with
            | Some snapshot -> return snapshot
            | None -> return! emptySnapshot store
        }

    let private asAppendStorage (error: StorageInvalid) : AppendError = AppendError.StorageInvalid error

    let private asPublishStorage (error: StorageInvalid) : PublishError = PublishError.StorageInvalid error

    let private expectedOldFor (store: IGitRawStore) (baseSnapshot: StoreSnapshot) : Task<GitObjectId option> =
        task {
            match! store.ReadRef StoreRef.canonical with
            | None -> return None
            | Some _ -> return Some(RootOid.value baseSnapshot.RootOid)
        }

    let private unionOnto
        (store: IGitRawStore)
        (baseSnapshot: StoreSnapshot)
        (events: EventEnvelope list)
        : Task<Result<StoreSnapshot, StorageInvalid>> =
        task {
            match events with
            | [] -> return Ok baseSnapshot
            | _ ->
                match! GitRawStore.materializeSnapshot store events with
                | Error err -> return Error err
                | Ok delta ->
                    match! EventStoreMerge.merge store (MergeInput.ofList [ baseSnapshot; delta ]) with
                    | Ok merged -> return Ok merged
                    | Error(MergeError.StorageInvalid err) -> return Error err
        }

    let private eventsAlreadyCommitted
        (store: IGitRawStore)
        (snapshot: StoreSnapshot)
        (events: EventEnvelope list)
        : Task<Result<bool, StorageInvalid>> =
        let rec loop remaining =
            task {
                match remaining with
                | [] -> return Ok true
                | head :: tail ->
                    let normalized = EventEnvelope.normalize head

                    match! GitRawStore.tryReadEvent store snapshot.RootOid normalized.EventId with
                    | Error err -> return Error err
                    | Ok None -> return Ok false
                    | Ok(Some existing) ->
                        match CanonicalEventCodec.checkIdentity normalized existing with
                        | Error err -> return Error err
                        | Ok() -> return! loop tail
            }

        loop events

    /// Incremental append validation — O(|new|) against the tip, not O(|history|).
    ///
    /// Prior shape reloaded every event via `loadAllEvents` + full `EventStoreFold.validate`,
    /// so AgentJournal append latency grew linearly with history (~85ms@1 → ~500ms@40) and
    /// Finality Perfect (2 appends) burned ~800–960ms. Snapshot projection is already cached;
    /// do not re-fold the store on every CAS attempt.
    let private validateAppendSet
        (store: IGitRawStore)
        (baseSnapshot: StoreSnapshot)
        (events: EventEnvelope list)
        : Task<Result<EventEnvelope list, StorageInvalid>> =
        task {
            match events with
            | [] -> return Ok []
            | _ ->
                match CanonicalEventCodec.mergeByIdentity events with
                | Error err -> return Error err
                | Ok normalized ->
                    let batchIds =
                        normalized
                        |> List.map (fun envelope -> EventId.value envelope.EventId)
                        |> Set.ofList

                    let rec checkVocabulary remaining =
                        match remaining with
                        | [] -> Ok()
                        | head :: tail ->
                            if AuthoritativeEventTypes.isKnown head.EventType then
                                checkVocabulary tail
                            else
                                Error(StorageInvalid.UnknownEventType head.EventType)

                    let rec checkIdentities remaining =
                        task {
                            match remaining with
                            | [] -> return Ok()
                            | head :: tail ->
                                let normalizedHead = EventEnvelope.normalize head

                                match! GitRawStore.tryReadEvent store baseSnapshot.RootOid normalizedHead.EventId with
                                | Error err -> return Error err
                                | Ok None -> return! checkIdentities tail
                                | Ok(Some existing) ->
                                    match CanonicalEventCodec.checkIdentity normalizedHead existing with
                                    | Error err -> return Error err
                                    | Ok() -> return! checkIdentities tail
                        }

                    let rec checkParents remaining =
                        task {
                            match remaining with
                            | [] -> return Ok()
                            | head :: tail ->
                                let rec parentsLeft parents =
                                    task {
                                        match parents with
                                        | [] -> return! checkParents tail
                                        | parent :: rest ->
                                            if Set.contains (EventId.value parent) batchIds then
                                                return! parentsLeft rest
                                            else
                                                match! GitRawStore.tryReadEvent store baseSnapshot.RootOid parent with
                                                | Error err -> return Error err
                                                | Ok None -> return Error(StorageInvalid.MissingParent parent)
                                                | Ok(Some _) -> return! parentsLeft rest
                                    }

                                return! parentsLeft head.Parents
                        }

                    /// Intra-batch cycle detection. Parents outside the batch are store-backed
                    /// (checked above) and do not contribute indegree — see Fold.validateBatchDag.
                    let checkBatchDag (batch: EventEnvelope list) : Result<unit, StorageInvalid> =
                        match EventStoreFold.validateBatchDag batch with
                        | Error(FoldError.StorageInvalid err) -> Error err
                        | Ok() -> Ok()

                    match checkVocabulary normalized with
                    | Error err -> return Error err
                    | Ok() ->
                        match! checkIdentities normalized with
                        | Error err -> return Error err
                        | Ok() ->
                            match! checkParents normalized with
                            | Error err -> return Error err
                            | Ok() ->
                                match checkBatchDag normalized with
                                | Error err -> return Error err
                                | Ok() -> return Ok normalized
        }

    let append
        (store: IGitRawStore)
        (maxRetries: int)
        (baseSnapshot: StoreSnapshot)
        (events: EventEnvelope list)
        : Task<Result<StoreSnapshot, AppendError>> =
        let rec loop (baseSnap: StoreSnapshot) (retriesLeft: int) =
            task {
                match! validateAppendSet store baseSnap events with
                | Error err -> return Error(asAppendStorage err)
                | Ok _ ->
                    match! unionOnto store baseSnap events with
                    | Error err -> return Error(asAppendStorage err)
                    | Ok candidate ->
                        let! expected = expectedOldFor store baseSnap
                        let newOid = RootOid.value candidate.RootOid
                        let! swapped = store.CompareAndSwapRef(StoreRef.canonical, expected, newOid)

                        if swapped then
                            return Ok candidate
                        elif retriesLeft <= 0 then
                            if maxRetries <= 0 then
                                return Error AppendError.AppendCasRejected
                            else
                                return Error AppendError.AppendRetryExhausted
                        else
                            match! readPublished store with
                            | None ->
                                let! empty = emptySnapshot store
                                return! loop empty (retriesLeft - 1)
                            | Some current ->
                                match! eventsAlreadyCommitted store current events with
                                | Error err -> return Error(asAppendStorage err)
                                | Ok true -> return Ok current
                                | Ok false -> return! loop current (retriesLeft - 1)
            }

        loop baseSnapshot maxRetries

    let publish
        (store: IGitRawStore)
        (maxRetries: int)
        (candidate: AppendCandidate)
        : Task<Result<StoreSnapshot, PublishError>> =
        let writePayloads (payloads: (GitObjectId * byte[]) list) : Task<Result<unit, PublishError>> =
            let rec loop remaining =
                task {
                    match remaining with
                    | [] -> return Ok()
                    | (expectedOid, bytes) :: tail ->
                        let! written = store.WriteBlob bytes

                        if GitObjectId.value written <> GitObjectId.value expectedOid then
                            return Error PublishError.IncompletePayloadClosure
                        else
                            return! loop tail
                }

            loop payloads

        task {
            match! writePayloads candidate.NewPayloads with
            | Error e -> return Error e
            | Ok() ->
                let closure = PayloadClosure.ofEvents candidate.NewEvents

                match! PayloadClosure.validatePresent store closure with
                | Error(StorageInvalid.MissingPayload _) -> return Error PublishError.IncompletePayloadClosure
                | Error err -> return Error(asPublishStorage err)
                | Ok() ->
                    match! append store maxRetries candidate.BaseSnapshot candidate.NewEvents with
                    | Ok snapshot -> return Ok snapshot
                    | Error(AppendError.StorageInvalid err) -> return Error(asPublishStorage err)
                    | Error AppendError.AppendCasRejected -> return Error PublishError.PublishCasRejected
                    | Error AppendError.AppendRetryExhausted -> return Error PublishError.PublishRetryExhausted
        }

    let merge (store: IGitRawStore) (snapshots: StoreSnapshot list) : Task<Result<StoreSnapshot, MergeError>> =
        EventStoreMerge.merge store (MergeInput.ofList snapshots)

    /// Unbound Converge — Application must compose with GitGateway (§11 / Phase 3 Wave A).
    let unboundConverge (remote: string) : Task<Result<StoreSnapshot, ConvergeError>> =
        Task.FromResult(Error(ConvergeError.Transport(sprintf "no GitGateway bound for Converge(%s)" remote)))

    let createWithRetries
        (store: IGitRawStore)
        (maxRetries: int)
        (convergeRemote: string -> Task<Result<StoreSnapshot, ConvergeError>>)
        : IEventStore =
        { new IEventStore with
            member _.OpenSnapshot() = openSnapshot store

            member _.Append(baseSnapshot, events) =
                append store maxRetries baseSnapshot events

            member _.Refresh() = openSnapshot store

            member _.Merge(snapshots) = merge store snapshots

            member _.Publish(candidate) = publish store maxRetries candidate

            member _.Converge(remote) = convergeRemote remote }

    /// Inject a converge delegate (GitGateway path or test fake).
    let createWithConverge
        (store: IGitRawStore)
        (maxRetries: int)
        (convergeRemote: string -> Task<Result<StoreSnapshot, ConvergeError>>)
        : IEventStore =
        createWithRetries store maxRetries convergeRemote

    let create (store: IGitRawStore) : IEventStore =
        createWithRetries store DefaultMaxRetries unboundConverge

    let createRejectingCas (inner: IGitRawStore) (maxRetries: int) : IEventStore =
        createWithRetries (CasRejectGitRawStore(inner) :> IGitRawStore) maxRetries unboundConverge
