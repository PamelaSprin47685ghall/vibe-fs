namespace Wanxiangshu.Persistence.EventStore

open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module EventStore =

    [<RequireQualifiedAccess>]
    type private AppendWork =
        | Rejected of AppendPreWriteRejection
        | NotAttempted of AppendNotAttemptedEvidence
        | Unknown of AppendCommitUnknownEvidence
        | NoNewWrite of AppendReceipt * PreparedAppend option
        | NewWrite of AppendReceipt * PreparedAppend

    let private tryPreparation action =
        try
            Ok(action ())
        with cause ->
            Error
                { Phase = AppendPhase.Preparation
                  Cause = cause }

    let private notAttempted requested prepared primary =
        AppendWork.NotAttempted
            { Requested = requested
              Prepared = prepared
              Primary = primary
              CleanupFailures = []
              PriorRejection = None }

    let private validateVocabulary
        (integrator: ICanonicalIntegrator)
        (events: EventEnvelope list)
        : Result<unit, StorageInvalid> =
        events
        |> List.tryFind (fun head -> not (integrator.IsEventTypeKnown head.EventType))
        |> Option.map (fun head -> Error(StorageInvalid.UnknownEventType head.EventType))
        |> Option.defaultValue (Ok())

    let private validateBatchDag (events: EventEnvelope list) : Result<unit, StorageInvalid> =
        let keys =
            events |> List.map (fun event -> EventId.value event.EventId) |> Set.ofList

        let parentsById =
            events
            |> List.map (fun event ->
                EventId.value event.EventId,
                event.Parents
                |> List.map EventId.value
                |> List.filter (fun parent -> Set.contains parent keys))
            |> Map.ofList

        let rec visit key visiting visited =
            if Set.contains key visited then
                Ok visited
            elif Set.contains key visiting then
                Error StorageInvalid.CyclicParents
            else
                walkChildren key visiting visited

        and walkChildren key visiting visited =
            let nextVisiting = Set.add key visiting
            let parents = Map.tryFind key parentsById |> Option.defaultValue []

            let rec visitParents remaining currentVisited =
                match remaining with
                | [] -> Ok(Set.add key currentVisited)
                | parent :: tail ->
                    result {
                        let! nextVisited = visit parent nextVisiting currentVisited
                        return! visitParents tail nextVisited
                    }

            visitParents parents visited

        let rec all remaining visited =
            match remaining with
            | [] -> Ok()
            | key :: tail ->
                result {
                    let! nextVisited = visit key Set.empty visited
                    return! all tail nextVisited
                }

        all (Set.toList keys) Set.empty

    let private reuseSeenIdentity
        (normalized: EventEnvelope)
        (existing: EventEnvelope)
        (seen: Map<string, EventEnvelope>)
        (acc: EventEnvelope list)
        : Result<Map<string, EventEnvelope> * EventEnvelope list, StorageInvalid> =
        result {
            do! CanonicalEventCodec.checkIdentity normalized existing
            return seen, acc
        }

    let private observeStoreOrFresh
        (integrator: ICanonicalIntegrator)
        (key: string)
        (normalized: EventEnvelope)
        (seen: Map<string, EventEnvelope>)
        (acc: EventEnvelope list)
        : Result<Map<string, EventEnvelope> * EventEnvelope list, StorageInvalid> =
        match integrator.TryEvent normalized.EventId with
        | Some existing ->
            result {
                do! CanonicalEventCodec.checkIdentity normalized existing
                return Map.add key normalized seen, acc
            }
        | None -> Ok(Map.add key normalized seen, normalized :: acc)

    let private stepAgainstCurrent
        (integrator: ICanonicalIntegrator)
        (normalized: EventEnvelope)
        (seen: Map<string, EventEnvelope>)
        (acc: EventEnvelope list)
        : Result<Map<string, EventEnvelope> * EventEnvelope list, StorageInvalid> =
        let key = EventId.value normalized.EventId

        match Map.tryFind key seen with
        | Some existing -> reuseSeenIdentity normalized existing seen acc
        | None -> observeStoreOrFresh integrator key normalized seen acc

    let private newEventsAgainstCurrent
        (integrator: ICanonicalIntegrator)
        (events: EventEnvelope list)
        : Result<EventEnvelope list, StorageInvalid> =
        let rec loop remaining seen acc =
            match remaining with
            | [] -> Ok(List.rev acc)
            | head :: tail ->
                result {
                    let! nextSeen, nextAcc = stepAgainstCurrent integrator (EventEnvelope.normalize head) seen acc

                    return! loop tail nextSeen nextAcc
                }

        loop events Map.empty []

    let private parentKnown (integrator: ICanonicalIntegrator) (batchIds: Set<string>) (parent: EventId) =
        Set.contains (EventId.value parent) batchIds
        || Option.isSome (integrator.TryEvent parent)

    let private validateParentList
        (integrator: ICanonicalIntegrator)
        (batchIds: Set<string>)
        (parents: EventId list)
        : Result<unit, StorageInvalid> =
        let rec loop remaining =
            match remaining with
            | [] -> Ok()
            | parent :: tail when parentKnown integrator batchIds parent -> loop tail
            | parent :: _ -> Error(StorageInvalid.MissingParent parent)

        loop parents

    let private validateParents
        (integrator: ICanonicalIntegrator)
        (events: EventEnvelope list)
        : Result<unit, StorageInvalid> =
        let batchIds =
            events
            |> List.map (fun envelope -> EventId.value envelope.EventId)
            |> Set.ofList

        events
        |> List.traverseResultM (fun head -> validateParentList integrator batchIds head.Parents)
        |> Result.map ignore

    let private validatePayloadClosure (commonDir: string) (events: EventEnvelope list) : Result<unit, StorageInvalid> =
        let refs =
            events
            |> List.collect (fun envelope -> envelope.PayloadRefs)
            |> PayloadRefs.canonicalize

        match refs |> List.tryFind (ProcessEventLog.payloadExists commonDir >> not) with
        | Some missing -> Error(StorageInvalid.MissingPayload missing)
        | None -> Ok()

    let private validateFreshBatch
        (commonDir: string)
        (integrator: ICanonicalIntegrator)
        (fresh: EventEnvelope list)
        : Result<EventEnvelope list, StorageInvalid> =
        result {
            do! validateParents integrator fresh
            do! validateBatchDag fresh
            do! validatePayloadClosure commonDir fresh
            return fresh
        }

    let private validateForAppend
        (commonDir: string)
        (integrator: ICanonicalIntegrator)
        (events: EventEnvelope list)
        : Result<EventEnvelope list, StorageInvalid> =
        result {
            do! validateVocabulary integrator events
            let! fresh = newEventsAgainstCurrent integrator events

            if List.isEmpty fresh then
                return []
            else
                return! validateFreshBatch commonDir integrator fresh
        }

    let private completedAppend receipt prepared completion =
        match completion with
        | ProcessEventLog.PhysicalAppendCompletion.NoAppend -> AppendWork.NoNewWrite(receipt, Some prepared)
        | ProcessEventLog.PhysicalAppendCompletion.AppendDurable -> AppendWork.NewWrite(receipt, prepared)

    let private currentCommitFailure requested prepared completion cause =
        let primary =
            { Phase = AppendPhase.CurrentCommit
              Cause = cause }

        match completion with
        | ProcessEventLog.PhysicalAppendCompletion.NoAppend -> notAttempted requested (Some prepared) primary
        | ProcessEventLog.PhysicalAppendCompletion.AppendDurable ->
            AppendWork.Unknown
                { Requested = requested
                  Prepared = prepared
                  Primary = primary
                  CleanupFailures = [] }

    let private commitCurrent requested (integration: PreparedIntegration) prepared completion =
        try
            integration.Commit()
            completedAppend { Cuts = integration.Cuts } prepared completion
        with cause ->
            currentCommitFailure requested prepared completion cause

    let private commitPrepared requested (log: ProcessEventLog) (integration: PreparedIntegration) =
        let prepared: PreparedAppend =
            { DurableEvents = integration.DurableEvents
              Cuts = integration.Cuts }

        match ProcessEventLog.append log prepared.DurableEvents with
        | Ok completion -> commitCurrent requested integration prepared completion
        | Error(ProcessEventLog.PhysicalAppendFailure.BeforeAppend primary) ->
            notAttempted requested (Some prepared) primary
        | Error(ProcessEventLog.PhysicalAppendFailure.AfterAppend(primary, cleanup)) ->
            AppendWork.Unknown
                { Requested = requested
                  Prepared = prepared
                  Primary = primary
                  CleanupFailures = cleanup }

    let private appendFresh
        (integrator: ICanonicalIntegrator)
        (log: ProcessEventLog)
        (requested: EventEnvelope list)
        (fresh: EventEnvelope list)
        : AppendWork =
        match tryPreparation (fun () -> integrator.PrepareLive fresh) with
        | Error primary -> notAttempted requested None primary
        | Ok(Error reason) ->
            AppendWork.Rejected(
                AppendPreWriteRejection.PreparationRejected("integration preparation failed: " + reason)
            )
        | Ok(Ok prepared) -> commitPrepared requested log prepared

    let private appendValidated
        (commonDir: string)
        (integrator: ICanonicalIntegrator)
        (log: ProcessEventLog)
        (events: EventEnvelope list)
        : AppendWork =
        match tryPreparation (fun () -> validateForAppend commonDir integrator events) with
        | Error primary -> notAttempted events None primary
        | Ok(Error invalid) -> AppendWork.Rejected(AppendPreWriteRejection.StorageInvalid invalid)
        | Ok(Ok []) -> AppendWork.NoNewWrite(AppendReceipt.empty, None)
        | Ok(Ok fresh) -> appendFresh integrator log events fresh

    let private rejectionError rejection =
        match rejection with
        | AppendPreWriteRejection.StorageInvalid invalid -> AppendError.StorageInvalid invalid
        | AppendPreWriteRejection.PreparationRejected reason -> AppendError.AppendFailed reason

    let private settleWork work =
        match work with
        | AppendWork.Rejected rejection -> Error(rejectionError rejection)
        | AppendWork.NotAttempted evidence -> Error(AppendError.AppendNotAttempted evidence)
        | AppendWork.Unknown evidence -> Error(AppendError.CommitUnknown evidence)
        | AppendWork.NoNewWrite(receipt, _)
        | AppendWork.NewWrite(receipt, _) -> Ok receipt

    let private settleReleaseFailure requested work cause =
        let release =
            { Phase = AppendPhase.StoreRelease
              Cause = cause }

        match work with
        | AppendWork.Rejected rejection ->
            Error(
                AppendError.AppendNotAttempted
                    { Requested = requested
                      Prepared = None
                      Primary = release
                      CleanupFailures = []
                      PriorRejection = Some rejection }
            )
        | AppendWork.NotAttempted evidence ->
            Error(
                AppendError.AppendNotAttempted
                    { evidence with
                        CleanupFailures = evidence.CleanupFailures @ [ release ] }
            )
        | AppendWork.Unknown evidence ->
            Error(
                AppendError.CommitUnknown
                    { evidence with
                        CleanupFailures = evidence.CleanupFailures @ [ release ] }
            )
        | AppendWork.NoNewWrite(_, prepared) ->
            Error(
                AppendError.NoNewWriteReleaseFailed
                    { Requested = requested
                      Prepared = prepared
                      Cause = cause }
            )
        | AppendWork.NewWrite(_, prepared) ->
            Error(
                AppendError.CommitUnknown
                    { Requested = requested
                      Prepared = prepared
                      Primary = release
                      CleanupFailures = [] }
            )

    let private acquireAppendGate commonDir =
        task {
            try
                let! acquired = ProcessEventLog.acquireStoreLock commonDir
                return Ok acquired
            with cause ->
                return
                    Error
                        { Phase = AppendPhase.GateAcquire
                          Cause = cause }
        }

    let private releaseAppendGate (acquired: StoreFileGate) =
        task {
            try
                do! acquired.Release()
                return Ok()
            with cause ->
                return Error cause
        }

    let private settleReleased requested work released =
        match released with
        | Ok() -> settleWork work
        | Error cause -> settleReleaseFailure requested work cause

    let private appendOwned commonDir integrator log gate requested =
        task {
            match! acquireAppendGate commonDir with
            | Error primary -> return notAttempted requested None primary |> settleWork
            | Ok acquired ->
                let work = lock gate (fun () -> appendValidated commonDir integrator log requested)
                let! released = releaseAppendGate acquired

                return settleReleased requested work released
        }

    let createLocal (commonDir: string) (writerId: string) (integrator: ICanonicalIntegrator) : IEventStore =
        let log = ProcessEventLog.create commonDir writerId
        let gate = obj ()

        let reloadFromDisk () = integrator.ReloadLocal commonDir

        match reloadFromDisk () with
        | Error error -> failwith ("local EventStore boot failed: " + error)
        | Ok() -> ()

        { new IEventStore with
            member _.Append(events) =
                appendOwned commonDir integrator log gate events

            member _.WritePayload(content) =
                ProcessEventLog.withStoreLock commonDir (fun () ->
                    task {
                        try
                            return Ok(ProcessEventLog.writePayload commonDir content)
                        with ex ->
                            return Error ex.Message
                    })

            member _.ReadPayload(payloadRef) =
                task {
                    try
                        return Ok(ProcessEventLog.readPayload commonDir payloadRef)
                    with ex ->
                        return Error ex.Message
                }

            member _.TryCurrent(key) = integrator.TryCurrent key
            member _.TryEvent(eventId) = integrator.TryEvent eventId
            member _.TryHeads(streamId) = integrator.TryHeads streamId
            member _.TryHead(streamId) = integrator.TryHead streamId
            member _.AllHeads() = integrator.AllHeads()
            member _.ReloadLocal() = reloadFromDisk () }
