namespace Wanxiangshu.Finality

open System
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Journal
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity

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
        task {
            match journal with
            | None -> return FinalityOutcome.Undecided(undecidedPrompt managerSessionId)
            | Some durable ->
                try
                    let snapshot = AgentJournal.snapshot durable

                    let lifeOpt =
                        AgentProjection.tryFind managerSessionId snapshot.AgentProjections
                        |> Option.bind (fun session -> session.ManagerLife)
                        |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)
                        |> Option.filter (fun life -> life.LifeId = lifeId)

                    let existingRequest =
                        lifeOpt
                        |> Option.bind (fun life -> life.ActiveFinality)
                        |> Option.filter (fun request -> request.RequestId = requestId)

                    let requestOpt =
                        match lifeOpt, existingRequest with
                        | _, Some request -> Some request
                        | Some _, None ->
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

                            AgentProjection.tryFind managerSessionId (AgentJournal.snapshot durable).AgentProjections
                            |> Option.bind (fun session -> session.ManagerLife)
                            |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)
                            |> Option.bind (fun life -> life.ActiveFinality)
                            |> Option.filter (fun request -> request.RequestId = requestId)
                        | None, None -> None

                    match lifeOpt, requestOpt with
                    | Some life, Some request ->
                        match!
                            CohortWorkflow.enlistRequiredReviewers reviewerPort durable managerSessionId life request
                        with
                        | Error _ ->
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
                        | Ok members ->
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

                                let siblings =
                                    match RevisionWorkflow.tryActiveFinality before managerSessionId requestId with
                                    | Some activeRequest ->
                                        RevisionWorkflow.durableRevisionSiblings before activeRequest reviewerId
                                        @ fromRace
                                        |> List.distinctBy fst
                                    | None -> fromRace

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
                                let reviewer, barrier =
                                    match members with
                                    | first :: _ -> first.ReviewerSessionId, first.BarrierId
                                    | [] -> undecidedMember managerSessionId request

                                return!
                                    RevisionWorkflow.concludeUndecided
                                        durable
                                        managerSessionId
                                        lifeId
                                        requestId
                                        requestTree
                                        reviewer
                                        barrier
                    | _ -> return FinalityOutcome.Undecided(undecidedPrompt managerSessionId)
                with _ ->
                    return FinalityOutcome.Undecided(undecidedPrompt managerSessionId)
        }

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
