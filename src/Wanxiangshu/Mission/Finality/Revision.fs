namespace Wanxiangshu.Mission.Finality

open Wanxiangshu.Change
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength.Persistence

open System.Threading.Tasks
open FsToolkit.ErrorHandling
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
open Wanxiangshu.Resources
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity

/// Finality rejection, sibling accounting/steering, and durable resume.
module RevisionWorkflow =

    let private infrastructureFailed operation (error: string) : 'T =
        invalidOp (sprintf "Finality %s failed: %s" operation error)

    let private rejectedPrompt (managerSessionId: SessionId) (workRecord: string) =
        FinalityPrompt.rejected
            (ProviderProse.documentFor managerSessionId FinalityPrompt.Path.Rejected Map.empty)
            workRecord

    let private steerPrompt (managerSessionId: SessionId) (workRecord: string) =
        FinalityPrompt.steer (ProviderProse.documentFor managerSessionId FinalityPrompt.Path.Steer Map.empty) workRecord

    let private steerUnavailablePrompt (managerSessionId: SessionId) =
        ProviderProse.documentFor managerSessionId FinalityPrompt.Path.SteerUnavailable Map.empty

    let private stagePrimaryRejectionRecord
        (journal: AgentJournal)
        (rejectingReviewer: SessionId)
        (barrierId: ReviewBarrierId)
        : Task<Result<string * BlobWriteReceipt, string>> =
        taskResult {
            let! workRecord = RecordWorkflow.awaitCanonicalWorkRecord journal rejectingReviewer barrierId
            let! blob = journal.WriteBlob workRecord
            return workRecord, blob
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

    [<RequireQualifiedAccess>]
    type private SiblingPollOutcome =
        | Complete of (SessionId * ReviewBarrierId * string) list
        | AwaitJournal
        | Failed of string

    let private collectSiblingStates
        (journal: AgentJournal)
        (siblings: (SessionId * ReviewBarrierId) list)
        : Task<JournalRevision * (SessionId * ReviewBarrierId * RecordReadiness) list> =
        task {
            let snapshot, revision = AgentJournal.snapshotWithRevision journal
            // DSL-MUTABLE: algorithm-scratch — sibling readiness accumulator
            let states = ResizeArray<SessionId * ReviewBarrierId * RecordReadiness>()

            for sid, barrierId in siblings do
                let! state = RecordWorkflow.readiness journal snapshot sid barrierId true
                states.Add(sid, barrierId, state)

            return revision, states |> Seq.toList
        }

    let private unavailableSiblingReason (states: (SessionId * ReviewBarrierId * RecordReadiness) list) =
        states
        |> List.tryPick (fun (_, _, state) ->
            match state with
            | RecordReadiness.Unavailable reason -> Some reason
            | _ -> None)

    let private readySiblingRecords (states: (SessionId * ReviewBarrierId * RecordReadiness) list) =
        states
        |> List.choose (fun (sid, barrierId, state) ->
            match state with
            | RecordReadiness.Ready record -> Some(sid, barrierId, record)
            | _ -> None)

    let private completeOrIncompleteSiblingRecords
        (states: (SessionId * ReviewBarrierId * RecordReadiness) list)
        (siblings: (SessionId * ReviewBarrierId) list)
        =
        let records = readySiblingRecords states

        if List.length records = List.length siblings then
            SiblingPollOutcome.Complete records
        else
            SiblingPollOutcome.Failed "durable sibling record readiness is incomplete"

    let private decideSiblingPoll
        (states: (SessionId * ReviewBarrierId * RecordReadiness) list)
        (siblings: (SessionId * ReviewBarrierId) list)
        =
        let awaitingJournal =
            states
            |> List.exists (fun (_, _, state) -> state = RecordReadiness.AwaitJournal)

        match unavailableSiblingReason states, awaitingJournal with
        | Some reason, _ -> SiblingPollOutcome.Failed reason
        | None, true -> SiblingPollOutcome.AwaitJournal
        | None, false -> completeOrIncompleteSiblingRecords states siblings

    let private awaitDurableSiblingRecords
        (journal: AgentJournal)
        (siblings: (SessionId * ReviewBarrierId) list)
        : Task<Result<(SessionId * ReviewBarrierId * string) list, string>> =
        let rec loop () =
            task {
                let! revision, states = collectSiblingStates journal siblings

                match decideSiblingPoll states siblings with
                | SiblingPollOutcome.Failed reason -> return Error reason
                | SiblingPollOutcome.AwaitJournal ->
                    let! _ = AgentJournal.awaitChangeFrom revision journal
                    return! loop ()
                | SiblingPollOutcome.Complete records -> return Ok records
            }

        task {
            if List.isEmpty siblings then
                return Ok []
            else
                return! loop ()
        }

    let tryActiveFinality (snapshot: ProjectionSet) (managerSessionId: SessionId) (requestId: FinalityRequestId) =
        AgentProjection.tryFind managerSessionId snapshot.AgentProjections
        |> Option.bind (fun session -> session.ManagerLife)
        |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)
        |> Option.bind (fun life -> life.ActiveFinality)
        |> Option.filter (fun active -> active.RequestId = requestId)

    let private prepareSiblingSteer
        (journal: AgentJournal)
        (existingSteers: Map<SessionId, SiblingSteerEvidence>)
        (reviewerSessionId: SessionId, barrierId: ReviewBarrierId, workRecord: string)
        : Task<Result<SessionId * ReviewBarrierId * string * BlobWriteReceipt option, string>> =
        match Map.tryFind reviewerSessionId existingSteers with
        | Some evidence ->
            taskResult {
                let! text = journal.Writer.BlobWriter.Read evidence.WorkRecordRef
                return reviewerSessionId, barrierId, text, None
            }
        | None ->
            taskResult {
                let! blob = journal.WriteBlob workRecord
                return reviewerSessionId, barrierId, workRecord, Some blob
            }

    let private appendNewSiblingSteerFact
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (requestTree: GitTreeHash)
        (reviewerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        (blob: BlobWriteReceipt)
        : Task<unit> =
        task {
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
        }

    let private commitSiblingSteerFacts
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (requestTree: GitTreeHash)
        (records: (SessionId * ReviewBarrierId * string) list)
        : Task<Result<(SessionId * string) list, string>> =
        taskResult {
            let existingSteers =
                tryActiveFinality (AgentJournal.snapshot journal) managerSessionId requestId
                |> Option.map (fun active -> active.SiblingSteers)
                |> Option.defaultValue Map.empty

            let! prepared = records |> TaskResultList.traverseM (prepareSiblingSteer journal existingSteers)

            let newFacts =
                prepared
                |> List.choose (fun (sid, barrierId, _, blobOpt) ->
                    blobOpt |> Option.map (fun blob -> sid, barrierId, blob))

            let! _ =
                newFacts
                |> TaskResultList.traverseM (fun (sid, barrierId, blob) ->
                    appendNewSiblingSteerFact journal managerSessionId lifeId requestId requestTree sid barrierId blob
                    |> TaskResultCE.ofTask)

            return prepared |> List.map (fun (sid, _, text, _) -> sid, text)
        }

    let private sendSiblingSteers
        (reviewerPort: FinalityReviewerPort)
        (managerSessionId: SessionId)
        (prepared: (SessionId * string) list)
        : Task =
        task {
            for _, workRecord in prepared do
                match! reviewerPort.SendRevisionSteer managerSessionId (steerPrompt managerSessionId workRecord) with
                | Ok() -> ()
                | Error error -> infrastructureFailed "revision sibling steer delivery" error
        }
        :> Task

    let private rejectAfterPrimary
        (reviewerPort: FinalityReviewerPort)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (rejectingReviewer: SessionId)
        (barrierId: ReviewBarrierId)
        (requestTree: GitTreeHash)
        (records: (SessionId * ReviewBarrierId * string) list)
        (workRecord: string)
        (primaryBlob: BlobWriteReceipt)
        : Task<FinalityOutcome> =
        task {
            match! commitSiblingSteerFacts journal managerSessionId lifeId requestId requestTree records with
            | Error error -> return infrastructureFailed "revision sibling accounting" error
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

    let private rejectAfterRecords
        (reviewerPort: FinalityReviewerPort)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (rejectingReviewer: SessionId)
        (barrierId: ReviewBarrierId)
        (requestTree: GitTreeHash)
        (records: (SessionId * ReviewBarrierId * string) list)
        : Task<FinalityOutcome> =
        task {
            match! stagePrimaryRejectionRecord journal rejectingReviewer barrierId with
            | Error error -> return infrastructureFailed "primary rejection record materialization" error
            | Ok(workRecord, primaryBlob) ->
                return!
                    rejectAfterPrimary
                        reviewerPort
                        journal
                        managerSessionId
                        lifeId
                        requestId
                        rejectingReviewer
                        barrierId
                        requestTree
                        records
                        workRecord
                        primaryBlob
        }

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
            | Error error -> return infrastructureFailed "revision sibling record readiness" error
            | Ok records ->
                return!
                    rejectAfterRecords
                        reviewerPort
                        journal
                        managerSessionId
                        lifeId
                        requestId
                        rejectingReviewer
                        barrierId
                        requestTree
                        records
        }

    let private readReadySteerRecord
        (journal: AgentJournal)
        (snapshot: ProjectionSet)
        (reviewerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        : Task<string option> =
        task {
            match! RecordWorkflow.readiness journal snapshot reviewerSessionId barrierId true with
            | RecordReadiness.Ready record -> return Some record
            | _ -> return None
        }

    let private readSteerWorkRecord
        (journal: AgentJournal)
        (snapshot: ProjectionSet)
        (reviewerSessionId: SessionId)
        (evidence: SiblingSteerEvidence)
        : Task<string option> =
        task {
            match! journal.Writer.BlobWriter.Read evidence.WorkRecordRef with
            | Ok workRecord -> return Some workRecord
            | Error _ -> return! readReadySteerRecord journal snapshot reviewerSessionId evidence.BarrierId
        }

    let private steerPromptOrUnavailable (managerSessionId: SessionId) (workRecordOpt: string option) =
        match workRecordOpt with
        | Some workRecord -> steerPrompt managerSessionId workRecord
        | None -> steerUnavailablePrompt managerSessionId

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
                let! workRecordOpt = readSteerWorkRecord journal snapshot reviewerSessionId evidence
                let prompt = steerPromptOrUnavailable managerSessionId workRecordOpt
                match! reviewerPort.SendRevisionSteer managerSessionId prompt with
                | Ok() -> ()
                | Error error -> infrastructureFailed "revision sibling steer replay" error
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

    let private tryRevisionBarrierId (memberRef: ReviewMemberRef) (guard: ReviewGuardProjection) =
        match guard.CurrentBarrierId, guard.Witness with
        | Some barrierId, ReviewWitness.RevisionWitness _ when barrierId = memberRef.BarrierId -> Some barrierId
        | _ -> None

    let private tryRevisionSibling
        (snapshot: ProjectionSet)
        (memberRef: ReviewMemberRef)
        (reviewerSessionId: SessionId)
        =
        AgentProjection.tryFind reviewerSessionId snapshot.AgentProjections
        |> Option.bind (fun session -> session.ReviewGuard)
        |> Option.bind (tryRevisionBarrierId memberRef)
        |> Option.map (fun barrierId -> reviewerSessionId, barrierId)

    let pendingRevision (snapshot: ProjectionSet) (request: FinalityRequestProjection) =
        request.Members
        |> Map.toList
        |> List.tryPick (fun (reviewerSessionId, memberRef) -> tryRevisionSibling snapshot memberRef reviewerSessionId)

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
                tryRevisionSibling snapshot memberRef reviewerSessionId)

    let private resumeOpenRejectedRequest
        (reviewerPort: FinalityReviewerPort)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (snapshot: ProjectionSet)
        (activeRequest: FinalityRequestProjection)
        : Task<FinalityOutcome option> =
        task {
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
        }

    let private resumeClosedRejectedRequest
        (reviewerPort: FinalityReviewerPort)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (requestId: FinalityRequestId)
        (activeRequest: FinalityRequestProjection)
        : Task<FinalityOutcome option> =
        task {
            let siblings =
                activeRequest.SiblingSteers
                |> Map.toList
                |> List.map (fun (sid, evidence) -> sid, evidence.BarrierId)

            if not (List.isEmpty siblings) then
                do! steerRevisionSiblings reviewerPort journal managerSessionId requestId siblings

            return None
        }

    let private resumeRejectedRequestBody
        (reviewerPort: FinalityReviewerPort)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        : Task<FinalityOutcome option> =
        task {
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
                return!
                    resumeOpenRejectedRequest
                        reviewerPort
                        journal
                        managerSessionId
                        lifeId
                        requestId
                        snapshot
                        activeRequest
            | Some activeRequest ->
                return! resumeClosedRejectedRequest reviewerPort journal managerSessionId requestId activeRequest
        }

    let resumeRejectedRequest
        (reviewerPort: FinalityReviewerPort)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        : Task<FinalityOutcome option> =
        resumeRejectedRequestBody reviewerPort journal managerSessionId lifeId requestId
