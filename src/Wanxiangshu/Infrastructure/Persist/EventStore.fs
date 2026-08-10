namespace Wanxiangshu.Infrastructure.Persist

open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity

/// Application-facing event store port (§2.4 / §9 / §10).
type IEventStore =
    abstract OpenSnapshot: unit -> StoreSnapshot
    abstract Append: baseSnapshot: StoreSnapshot * events: EventEnvelope list -> Result<StoreSnapshot, AppendError>
    abstract Refresh: unit -> StoreSnapshot
    abstract Merge: snapshots: StoreSnapshot list -> Result<StoreSnapshot, MergeError>
    abstract Publish: candidate: AppendCandidate -> Result<StoreSnapshot, PublishError>
    abstract Converge: remote: string -> Result<StoreSnapshot, ConvergeError>

/// Test double: forwards reads/writes; CompareAndSwapRef always rejects.
type CasRejectGitRawStore(inner: IGitRawStore) =
    interface IGitRawStore with
        member _.WriteBlob(content) = inner.WriteBlob content
        member _.WriteTree(entries) = inner.WriteTree entries
        member _.ReadObject(oid) = inner.ReadObject oid
        member _.ReadTree(oid) = inner.ReadTree oid
        member _.ReadRef(refName) = inner.ReadRef refName
        member _.CompareAndSwapRef(_, _, _) = false

[<RequireQualifiedAccess>]
module EventStore =
    [<Literal>]
    let DefaultMaxRetries = 8

    let private emptySnapshot (store: IGitRawStore) : StoreSnapshot =
        match GitRawStore.materializeSnapshot store [] with
        | Ok snapshot -> snapshot
        | Error err -> failwith (sprintf "empty store materialization failed: %A" err)

    let private readPublished (store: IGitRawStore) : StoreSnapshot option =
        store.ReadRef StoreRef.canonical
        |> Option.map (fun oid -> { RootOid = RootOid.create oid })

    let openSnapshot (store: IGitRawStore) : StoreSnapshot =
        match readPublished store with
        | Some snapshot -> snapshot
        | None -> emptySnapshot store

    let private asAppendStorage (error: StorageInvalid) : AppendError = AppendError.StorageInvalid error

    let private asPublishStorage (error: StorageInvalid) : PublishError = PublishError.StorageInvalid error

    let private expectedOldFor (store: IGitRawStore) (baseSnapshot: StoreSnapshot) : GitObjectId option =
        match store.ReadRef StoreRef.canonical with
        | None -> None
        | Some _ -> Some(RootOid.value baseSnapshot.RootOid)

    let private unionOnto
        (store: IGitRawStore)
        (baseSnapshot: StoreSnapshot)
        (events: EventEnvelope list)
        : Result<StoreSnapshot, StorageInvalid> =
        match events with
        | [] -> Ok baseSnapshot
        | _ ->
            match GitRawStore.materializeSnapshot store events with
            | Error err -> Error err
            | Ok delta ->
                match EventStoreMerge.merge store (MergeInput.ofList [ baseSnapshot; delta ]) with
                | Ok merged -> Ok merged
                | Error(MergeError.StorageInvalid err) -> Error err

    let private loadAllEvents
        (store: IGitRawStore)
        (snapshot: StoreSnapshot)
        : Result<EventEnvelope list, StorageInvalid> =
        match EventStoreMergeSpec.merge store (MergeInput.ofList [ snapshot ]) with
        | Ok events -> Ok events
        | Error(MergeError.StorageInvalid err) -> Error err

    let private eventsAlreadyCommitted
        (store: IGitRawStore)
        (snapshot: StoreSnapshot)
        (events: EventEnvelope list)
        : Result<bool, StorageInvalid> =
        let rec loop remaining =
            match remaining with
            | [] -> Ok true
            | head :: tail ->
                let normalized = EventEnvelope.normalize head

                match GitRawStore.tryReadEvent store snapshot.RootOid normalized.EventId with
                | Error err -> Error err
                | Ok None -> Ok false
                | Ok(Some existing) ->
                    match CanonicalEventCodec.checkIdentity normalized existing with
                    | Error err -> Error err
                    | Ok() -> loop tail

        loop events

    let private validateAppendSet
        (store: IGitRawStore)
        (baseSnapshot: StoreSnapshot)
        (events: EventEnvelope list)
        : Result<EventEnvelope list, StorageInvalid> =
        match loadAllEvents store baseSnapshot with
        | Error err -> Error err
        | Ok existing ->
            match CanonicalEventCodec.mergeByIdentity (existing @ events) with
            | Error err -> Error err
            | Ok unioned ->
                match EventStoreFold.validate unioned with
                | Error(FoldError.StorageInvalid err) -> Error err
                | Ok() -> Ok unioned

    let append
        (store: IGitRawStore)
        (maxRetries: int)
        (baseSnapshot: StoreSnapshot)
        (events: EventEnvelope list)
        : Result<StoreSnapshot, AppendError> =
        let rec loop (baseSnap: StoreSnapshot) (retriesLeft: int) =
            match validateAppendSet store baseSnap events with
            | Error err -> Error(asAppendStorage err)
            | Ok _ ->
                match unionOnto store baseSnap events with
                | Error err -> Error(asAppendStorage err)
                | Ok candidate ->
                    let expected = expectedOldFor store baseSnap
                    let newOid = RootOid.value candidate.RootOid

                    if store.CompareAndSwapRef(StoreRef.canonical, expected, newOid) then
                        Ok candidate
                    elif retriesLeft <= 0 then
                        if maxRetries <= 0 then
                            Error AppendError.AppendCasRejected
                        else
                            Error AppendError.AppendRetryExhausted
                    else
                        match readPublished store with
                        | None -> loop (emptySnapshot store) (retriesLeft - 1)
                        | Some current ->
                            match eventsAlreadyCommitted store current events with
                            | Error err -> Error(asAppendStorage err)
                            | Ok true -> Ok current
                            | Ok false -> loop current (retriesLeft - 1)

        loop baseSnapshot maxRetries

    let publish
        (store: IGitRawStore)
        (maxRetries: int)
        (candidate: AppendCandidate)
        : Result<StoreSnapshot, PublishError> =
        let writePayloads (payloads: (GitObjectId * byte[]) list) : Result<unit, PublishError> =
            let rec loop remaining =
                match remaining with
                | [] -> Ok()
                | (expectedOid, bytes) :: tail ->
                    let written = store.WriteBlob bytes

                    if GitObjectId.value written <> GitObjectId.value expectedOid then
                        Error PublishError.IncompletePayloadClosure
                    else
                        loop tail

            loop payloads

        match writePayloads candidate.NewPayloads with
        | Error e -> Error e
        | Ok() ->
            let closure = PayloadClosure.ofEvents candidate.NewEvents

            match PayloadClosure.validatePresent store closure with
            | Error(StorageInvalid.MissingPayload _) -> Error PublishError.IncompletePayloadClosure
            | Error err -> Error(asPublishStorage err)
            | Ok() ->
                match append store maxRetries candidate.BaseSnapshot candidate.NewEvents with
                | Ok snapshot -> Ok snapshot
                | Error(AppendError.StorageInvalid err) -> Error(asPublishStorage err)
                | Error AppendError.AppendCasRejected -> Error PublishError.PublishCasRejected
                | Error AppendError.AppendRetryExhausted -> Error PublishError.PublishRetryExhausted

    let merge (store: IGitRawStore) (snapshots: StoreSnapshot list) : Result<StoreSnapshot, MergeError> =
        EventStoreMerge.merge store (MergeInput.ofList snapshots)

    /// Unbound Converge — Application must compose with GitGateway (§11 / Phase 3 Wave A).
    let unboundConverge (remote: string) : Result<StoreSnapshot, ConvergeError> =
        Error(ConvergeError.Transport(sprintf "no GitGateway bound for Converge(%s)" remote))

    let createWithRetries
        (store: IGitRawStore)
        (maxRetries: int)
        (convergeRemote: string -> Result<StoreSnapshot, ConvergeError>)
        : IEventStore =
        { new IEventStore with
            member _.OpenSnapshot() = openSnapshot store

            member _.Append(baseSnapshot, events) =
                append store maxRetries baseSnapshot events

            member _.Refresh() = openSnapshot store

            member _.Merge(snapshots) = merge store snapshots

            member _.Publish(candidate) = publish store maxRetries candidate

            member _.Converge(remote) = convergeRemote remote }

    /// Inject a sync converge delegate (GitGateway sync path or test fake).
    let createWithConverge
        (store: IGitRawStore)
        (maxRetries: int)
        (convergeRemote: string -> Result<StoreSnapshot, ConvergeError>)
        : IEventStore =
        createWithRetries store maxRetries convergeRemote

    let create (store: IGitRawStore) : IEventStore =
        createWithRetries store DefaultMaxRetries unboundConverge

    let createRejectingCas (inner: IGitRawStore) (maxRetries: int) : IEventStore =
        createWithRetries (CasRejectGitRawStore(inner) :> IGitRawStore) maxRetries unboundConverge
