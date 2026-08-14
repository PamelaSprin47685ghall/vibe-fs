namespace Wanxiangshu.Finality

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Domain
open Wanxiangshu.Resources
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity

/// Finality rejection, sibling accounting/steering, and durable resume.
module RevisionWorkflow =

    let private undecidedPrompt (managerSessionId: SessionId) =
        ProviderProse.documentFor managerSessionId ManagerLifecyclePrompt.Path.FinalityUndecidable Map.empty

    let private rejectedPrompt (managerSessionId: SessionId) (workRecord: string) =
        FinalityPrompt.rejected
            (ProviderProse.documentFor managerSessionId FinalityPrompt.Path.Rejected Map.empty)
            workRecord

    let private steerPrompt (managerSessionId: SessionId) (workRecord: string) =
        FinalityPrompt.steer (ProviderProse.documentFor managerSessionId FinalityPrompt.Path.Steer Map.empty) workRecord

    let private steerUnavailablePrompt (managerSessionId: SessionId) =
        ProviderProse.documentFor managerSessionId FinalityPrompt.Path.SteerUnavailable Map.empty

    let concludeUndecided
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (requestTree: GitTreeHash)
        (reviewerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        : Task<FinalityOutcome> =
        task {
            do!
                FinalityJournal.appendLifecycle
                    journal
                    (ManagerLifecycleFact.FinalityUndecided
                        {| SessionId = managerSessionId
                           LifeId = lifeId
                           RequestId = requestId
                           ReviewerSessionId = reviewerSessionId
                           BarrierId = barrierId
                           GitTreeHash = requestTree |})

            return FinalityOutcome.Undecided(undecidedPrompt managerSessionId)
        }

    let private stagePrimaryRejectionRecord
        (journal: AgentJournal)
        (rejectingReviewer: SessionId)
        (barrierId: ReviewBarrierId)
        : Task<Result<string * BlobWriteReceipt, string>> =
        task {
            match! RecordWorkflow.awaitCanonicalWorkRecord journal rejectingReviewer barrierId with
            | Error reason -> return Error reason
            | Ok workRecord ->
                match! journal.WriteBlob workRecord with
                | Error reason -> return Error reason
                | Ok blob -> return Ok(workRecord, blob)
        }

    let private sealRejected
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (rejectingReviewer: SessionId)
        (barrierId: ReviewBarrierId)
        (requestTree: GitTreeHash)
        (workRecord: string)
        (blob: BlobWriteReceipt)
        : Task<FinalityOutcome> =
        task {
            do!
                FinalityJournal.appendLifecycle
                    journal
                    (ManagerLifecycleFact.FinalityRejected
                        {| SessionId = managerSessionId
                           LifeId = lifeId
                           RequestId = requestId
                           RejectingReviewerSessionId = rejectingReviewer
                           BarrierId = barrierId
                           GitTreeHash = requestTree
                           WorkRecordRef = blob.BlobRef
                           WorkRecordDigest = blob.BlobDigest |})

            return FinalityOutcome.Rejected(rejectedPrompt managerSessionId workRecord)
        }

    let private awaitDurableSiblingRecords
        (journal: AgentJournal)
        (siblings: (SessionId * ReviewBarrierId) list)
        : Task<Result<(SessionId * ReviewBarrierId * string) list, string>> =
        let rec loop () =
            task {
                if List.isEmpty siblings then
                    return Ok []
                else
                    let snapshot, revision = AgentJournal.snapshotWithRevision journal

                    let states = ResizeArray<SessionId * ReviewBarrierId * RecordReadiness>()

                    for sid, barrierId in siblings do
                        let! state = RecordWorkflow.readiness journal snapshot sid barrierId true
                        states.Add(sid, barrierId, state)

                    let states = states |> Seq.toList

                    match
                        states
                        |> List.tryPick (fun (_, _, state) ->
                            match state with
                            | RecordReadiness.Unavailable reason -> Some reason
                            | _ -> None)
                    with
                    | Some reason -> return Error reason
                    | None when
                        states
                        |> List.exists (fun (_, _, state) -> state = RecordReadiness.AwaitJournal)
                        ->
                        let! _ = AgentJournal.awaitChangeFrom revision journal
                        return! loop ()
                    | None ->
                        let records =
                            states
                            |> List.choose (fun (sid, barrierId, state) ->
                                match state with
                                | RecordReadiness.Ready record -> Some(sid, barrierId, record)
                                | _ -> None)

                        if List.length records = List.length siblings then
                            return Ok records
                        else
                            return Error "durable sibling record readiness is incomplete"
            }

        loop ()

    let tryActiveFinality (snapshot: ProjectionSet) (managerSessionId: SessionId) (requestId: FinalityRequestId) =
        AgentProjection.tryFind managerSessionId snapshot.AgentProjections
        |> Option.bind (fun session -> session.ManagerLife)
        |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)
        |> Option.bind (fun life -> life.ActiveFinality)
        |> Option.filter (fun active -> active.RequestId = requestId)

    let private commitSiblingSteerFacts
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (requestTree: GitTreeHash)
        (records: (SessionId * ReviewBarrierId * string) list)
        : Task<Result<(SessionId * string) list, string>> =
        task {
            let existingSteers =
                tryActiveFinality (AgentJournal.snapshot journal) managerSessionId requestId
                |> Option.map (fun active -> active.SiblingSteers)
                |> Option.defaultValue Map.empty

            let prepared =
                ResizeArray<SessionId * ReviewBarrierId * string * BlobWriteReceipt option>()
            // DSL-MUTABLE: algorithm-scratch — first preparation failure while preserving input order
            let mutable failure: string option = None

            for reviewerSessionId, barrierId, workRecord in records do
                if failure.IsNone then
                    match Map.tryFind reviewerSessionId existingSteers with
                    | Some evidence ->
                        match! journal.Writer.BlobWriter.Read evidence.WorkRecordRef with
                        | Ok text -> prepared.Add(reviewerSessionId, barrierId, text, None)
                        | Error reason -> failure <- Some reason
                    | None ->
                        match! journal.WriteBlob workRecord with
                        | Error reason -> failure <- Some reason
                        | Ok blob -> prepared.Add(reviewerSessionId, barrierId, workRecord, Some blob)

            match failure with
            | Some reason -> return Error reason
            | None ->
                for reviewerSessionId, barrierId, _, blobOpt in prepared do
                    match blobOpt with
                    | None -> ()
                    | Some blob ->
                        do!
                            FinalityJournal.appendLifecycle
                                journal
                                (ManagerLifecycleFact.FinalitySiblingSteered
                                    {| SessionId = managerSessionId
                                       LifeId = lifeId
                                       RequestId = requestId
                                       ReviewerSessionId = reviewerSessionId
                                       BarrierId = barrierId
                                       GitTreeHash = requestTree
                                       WorkRecordRef = blob.BlobRef
                                       WorkRecordDigest = blob.BlobDigest |})

                return Ok(prepared |> Seq.map (fun (sid, _, text, _) -> sid, text) |> Seq.toList)
        }

    let private sendSiblingSteers
        (reviewerPort: FinalityReviewerPort)
        (managerSessionId: SessionId)
        (prepared: (SessionId * string) list)
        : Task =
        task {
            for _, workRecord in prepared do
                let! _ = reviewerPort.SendRevisionSteer managerSessionId (steerPrompt managerSessionId workRecord)
                ()
        }
        :> Task

    let rejectAndSteer
        (reviewerPort: FinalityReviewerPort)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (rejectingReviewer: SessionId)
        (barrierId: ReviewBarrierId)
        (requestTree: GitTreeHash)
        (siblings: (SessionId * ReviewBarrierId) list)
        : Task<FinalityOutcome> =
        task {
            match! awaitDurableSiblingRecords journal siblings with
            | Error _ ->
                return!
                    concludeUndecided journal managerSessionId lifeId requestId requestTree rejectingReviewer barrierId
            | Ok records ->
                match! stagePrimaryRejectionRecord journal rejectingReviewer barrierId with
                | Error _ ->
                    return!
                        concludeUndecided
                            journal
                            managerSessionId
                            lifeId
                            requestId
                            requestTree
                            rejectingReviewer
                            barrierId
                | Ok(workRecord, primaryBlob) ->
                    match! commitSiblingSteerFacts journal managerSessionId lifeId requestId requestTree records with
                    | Error _ ->
                        return!
                            concludeUndecided
                                journal
                                managerSessionId
                                lifeId
                                requestId
                                requestTree
                                rejectingReviewer
                                barrierId
                    | Ok prepared ->
                        let! outcome =
                            sealRejected
                                journal
                                managerSessionId
                                lifeId
                                requestId
                                rejectingReviewer
                                barrierId
                                requestTree
                                workRecord
                                primaryBlob

                        do! sendSiblingSteers reviewerPort managerSessionId prepared
                        return outcome
        }

    let private replaySiblingSteer
        (reviewerPort: FinalityReviewerPort)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (requestId: FinalityRequestId)
        (reviewerSessionId: SessionId)
        : Task =
        task {
            let snapshot = AgentJournal.snapshot journal

            match
                tryActiveFinality snapshot managerSessionId requestId
                |> Option.bind (fun active -> Map.tryFind reviewerSessionId active.SiblingSteers)
            with
            | None -> ()
            | Some evidence ->
                let! workRecordOpt =
                    task {
                        match! journal.Writer.BlobWriter.Read evidence.WorkRecordRef with
                        | Ok workRecord -> return Some workRecord
                        | Error _ ->
                            match!
                                RecordWorkflow.readiness journal snapshot reviewerSessionId evidence.BarrierId true
                            with
                            | RecordReadiness.Ready record -> return Some record
                            | _ -> return None
                    }

                let prompt =
                    match workRecordOpt with
                    | Some workRecord -> steerPrompt managerSessionId workRecord
                    | None -> steerUnavailablePrompt managerSessionId

                let! _ = reviewerPort.SendRevisionSteer managerSessionId prompt
                ()
        }
        :> Task

    let steerRevisionSiblings
        (reviewerPort: FinalityReviewerPort)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (requestId: FinalityRequestId)
        (siblings: (SessionId * ReviewBarrierId) list)
        : Task =
        task {
            for reviewerSessionId, _ in siblings do
                do! replaySiblingSteer reviewerPort journal managerSessionId requestId reviewerSessionId
        }
        :> Task

    let pendingRevision (snapshot: ProjectionSet) (request: FinalityRequestProjection) =
        request.Members
        |> Map.toList
        |> List.tryPick (fun (reviewerSessionId, memberRef) ->
            AgentProjection.tryFind reviewerSessionId snapshot.AgentProjections
            |> Option.bind (fun session -> session.ReviewGuard)
            |> Option.bind (fun guard ->
                match guard.CurrentBarrierId, guard.Witness with
                | Some barrierId, ReviewWitness.RevisionWitness _ when barrierId = memberRef.BarrierId ->
                    Some(reviewerSessionId, barrierId)
                | _ -> None))

    let durableRevisionSiblings
        (snapshot: ProjectionSet)
        (request: FinalityRequestProjection)
        (rejectingReviewer: SessionId)
        =
        request.Members
        |> Map.toList
        |> List.choose (fun (reviewerSessionId, memberRef) ->
            if reviewerSessionId = rejectingReviewer then
                None
            else
                AgentProjection.tryFind reviewerSessionId snapshot.AgentProjections
                |> Option.bind (fun session -> session.ReviewGuard)
                |> Option.bind (fun guard ->
                    match guard.CurrentBarrierId, guard.Witness with
                    | Some barrierId, ReviewWitness.RevisionWitness _ when barrierId = memberRef.BarrierId ->
                        Some(reviewerSessionId, barrierId)
                    | _ -> None))

    let resumeRejectedRequest
        (reviewerPort: FinalityReviewerPort)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        : Task<FinalityOutcome option> =
        task {
            try
                let snapshot = AgentJournal.snapshot journal

                let requestOpt =
                    AgentProjection.tryFind managerSessionId snapshot.AgentProjections
                    |> Option.bind (fun session -> session.ManagerLife)
                    |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)
                    |> Option.filter (fun life -> life.LifeId = lifeId)
                    |> Option.bind (fun life -> life.ActiveFinality)
                    |> Option.filter (fun active -> active.RequestId = requestId)

                match requestOpt with
                | None -> return None
                | Some activeRequest when ManagerLifecycleProjection.isOpen activeRequest ->
                    match pendingRevision snapshot activeRequest with
                    | None -> return None
                    | Some(reviewerSessionId, barrierId) ->
                        let siblings =
                            durableRevisionSiblings snapshot activeRequest reviewerSessionId
                            @ (activeRequest.SiblingSteers
                               |> Map.toList
                               |> List.map (fun (sid, evidence) -> sid, evidence.BarrierId)
                               |> List.filter (fun (sid, _) -> sid <> reviewerSessionId))
                            |> List.distinctBy fst

                        let! outcome =
                            rejectAndSteer
                                reviewerPort
                                journal
                                managerSessionId
                                lifeId
                                requestId
                                reviewerSessionId
                                barrierId
                                activeRequest.GitTreeHash
                                siblings

                        return Some outcome
                | Some activeRequest ->
                    let siblings =
                        activeRequest.SiblingSteers
                        |> Map.toList
                        |> List.map (fun (sid, evidence) -> sid, evidence.BarrierId)

                    if not (List.isEmpty siblings) then
                        do! steerRevisionSiblings reviewerPort journal managerSessionId requestId siblings

                    return None
            with _ ->
                return Some(FinalityOutcome.Undecided(undecidedPrompt managerSessionId))
        }
