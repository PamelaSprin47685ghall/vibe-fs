namespace Wanxiangshu.Mission.Finality

open Wanxiangshu.Change
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength.Persistence

open System
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

/// Manager Finality story: enlist cohort → review → reject+steer OR bless.
module FinalityWorkflow =

    let private undecidedPrompt (managerSessionId: SessionId) =
        ProviderProse.documentFor managerSessionId ManagerLifecyclePrompt.Path.FinalityUndecidable Map.empty

    let private undecidedMember (managerSessionId: SessionId) (request: FinalityRequestProjection) =
        request.Members
        |> Map.toList
        |> List.tryHead
        |> Option.map (fun (reviewerSessionId, memberRef) -> reviewerSessionId, memberRef.BarrierId)
        |> Option.defaultValue (managerSessionId, ReviewBarrierId.create (Guid.NewGuid().ToString("N")))

    let private requestForLife
        (durable: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (requestTree: GitTreeHash)
        (lastWordsRef: BlobRef)
        (lastWordsDigest: BlobDigest)
        (providerRun: ProviderRunIdentity)
        (toolCallId: ToolCallId)
        (lifeOpt: LifeProjection option)
        (existingRequest: FinalityRequestProjection option) =
        match lifeOpt, existingRequest with
        | _, Some request -> Task.FromResult(Some request)
        | Some _, None ->
            task {
                do!
                    FinalityJournal.appendLifecycle
                        durable
                        (ManagerLifecycleFact.FinalityRequested
                            {| SessionId = managerSessionId
                               LifeId = lifeId
                               RequestId = requestId
                               GitTreeHash = requestTree
                               LastWordsRef = lastWordsRef
                               LastWordsDigest = lastWordsDigest
                               ProviderRun = providerRun
                               ToolCallId = toolCallId |})

                return
                    AgentProjection.tryFind managerSessionId (AgentJournal.snapshot durable).AgentProjections
                    |> Option.bind (fun session -> session.ManagerLife)
                    |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)
                    |> Option.bind (fun life -> life.ActiveFinality)
                    |> Option.filter (fun request -> request.RequestId = requestId)
            }
        | None, None -> Task.FromResult None

    let private revisionSiblings
        (snapshot: ProjectionSet)
        (managerSessionId: SessionId)
        (requestId: FinalityRequestId)
        reviewerId
        fromRace =
        match RevisionWorkflow.tryActiveFinality snapshot managerSessionId requestId with
        | Some activeRequest -> RevisionWorkflow.durableRevisionSiblings snapshot activeRequest reviewerId @ fromRace |> List.distinctBy fst
        | None -> fromRace

    let private reviewMembers
        (reviewerPort: FinalityReviewerPort)
        (treePort: FinalityTreePort)
        (durable: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (requestTree: GitTreeHash)
        (members: EnlistedMember list)
        (request: FinalityRequestProjection) =
        task {
            match!
                CohortWorkflow.reviewUntilFirstRevisionOrAllConfirmed
                    reviewerPort
                    durable
                    managerSessionId
                    members
                    requestTree
            with
            | CohortJudgement.RevisionRequired(reviewerId, barrierId, fromRace) ->
                let before = AgentJournal.snapshot durable
                let siblings = revisionSiblings before managerSessionId requestId reviewerId fromRace

                return!
                    RevisionWorkflow.rejectAndSteer
                        reviewerPort
                        durable
                        managerSessionId
                        lifeId
                        requestId
                        reviewerId
                        barrierId
                        requestTree
                        siblings
            | CohortJudgement.AllConfirmed ->
                return!
                    BlessingWorkflow.blessIfTreeUnchanged
                        reviewerPort
                        treePort
                        durable
                        managerSessionId
                        lifeId
                        requestId
                        members
                        requestTree
            | CohortJudgement.Undecided ->
                let reviewer, barrier = undecidedMember managerSessionId request
                return!
                    RevisionWorkflow.concludeUndecided
                        durable
                        managerSessionId
                        lifeId
                        requestId
                        requestTree
                        reviewer
                        barrier
        }

    let private enlistAndReview
        (reviewerPort: FinalityReviewerPort)
        (treePort: FinalityTreePort)
        (durable: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: LifeProjection)
        (request: FinalityRequestProjection)
        (lifeIdValue: ManagerLifeId)
        (requestId: FinalityRequestId)
        (requestTree: GitTreeHash) =
        task {
            match! CohortWorkflow.enlistRequiredReviewers reviewerPort durable managerSessionId lifeId request with
            | Error _ ->
                let reviewer, barrier = undecidedMember managerSessionId request
                return!
                    RevisionWorkflow.concludeUndecided
                        durable
                        managerSessionId
                        lifeIdValue
                        requestId
                        requestTree
                        reviewer
                        barrier
            | Ok members ->
                return!
                    reviewMembers
                        reviewerPort
                        treePort
                        durable
                        managerSessionId
                        lifeIdValue
                        requestId
                        requestTree
                        members
                        request
        }

    let private executeDurable
        (reviewerPort: FinalityReviewerPort)
        (treePort: FinalityTreePort)
        (durable: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (requestTree: GitTreeHash)
        (lastWordsRef: BlobRef)
        (lastWordsDigest: BlobDigest)
        (providerRun: ProviderRunIdentity)
        (toolCallId: ToolCallId) =
        task {
            let snapshot = AgentJournal.snapshot durable
            let lifeOpt =
                AgentProjection.tryFind managerSessionId snapshot.AgentProjections
                |> Option.bind (fun session -> session.ManagerLife)
                |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)
                |> Option.filter (fun life -> life.LifeId = lifeId)
            let existingRequest = lifeOpt |> Option.bind (fun life -> life.ActiveFinality) |> Option.filter (fun request -> request.RequestId = requestId)
            let! requestOpt = requestForLife durable managerSessionId lifeId requestId requestTree lastWordsRef lastWordsDigest providerRun toolCallId lifeOpt existingRequest

            match lifeOpt, requestOpt with
            | Some life, Some request -> return! enlistAndReview reviewerPort treePort durable managerSessionId life request lifeId requestId requestTree
            | _ -> return FinalityOutcome.Undecided(undecidedPrompt managerSessionId)
        }

    let private startDurable
        (reviewerPort: FinalityReviewerPort)
        (treePort: FinalityTreePort)
        (durable: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (requestTree: GitTreeHash)
        (lastWordsRef: BlobRef)
        (lastWordsDigest: BlobDigest)
        (providerRun: ProviderRunIdentity)
        (toolCallId: ToolCallId) =
        task {
            try
                return! executeDurable reviewerPort treePort durable managerSessionId lifeId requestId requestTree lastWordsRef lastWordsDigest providerRun toolCallId
            with _ ->
                return FinalityOutcome.Undecided(undecidedPrompt managerSessionId)
        }

    let start
        (reviewerPort: FinalityReviewerPort)
        (treePort: FinalityTreePort)
        (journal: AgentJournal option)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (requestTree: GitTreeHash)
        (lastWordsRef: BlobRef)
        (lastWordsDigest: BlobDigest)
        (providerRun: ProviderRunIdentity)
        (toolCallId: ToolCallId)
        : Task<FinalityOutcome> =
        match journal with
        | None -> Task.FromResult(FinalityOutcome.Undecided(undecidedPrompt managerSessionId))
        | Some durable ->
            startDurable
                reviewerPort
                treePort
                durable
                managerSessionId
                lifeId
                requestId
                requestTree
                lastWordsRef
                lastWordsDigest
                providerRun
                toolCallId

    let resume
        (reviewerPort: FinalityReviewerPort)
        (journal: AgentJournal option)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        : Task<FinalityOutcome option> =
        match journal with
        | None -> Task.FromResult None
        | Some durable -> RevisionWorkflow.resumeRejectedRequest reviewerPort durable managerSessionId lifeId requestId
