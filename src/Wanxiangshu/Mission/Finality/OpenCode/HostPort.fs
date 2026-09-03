namespace Wanxiangshu.Mission.Finality.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal

/// Physical OpenCode adapter for Application Finality workflows.
module FinalityHostPort =

    let create
        (scope: ToolRuntimeScope)
        (managerSessionId: SessionId)
        (reviewerTimeoutMs: int)
        : FinalityReviewerPort * FinalityTreePort =
        let runtime =
            HostForkRuntime(
                managerSessionId,
                scope.Sessions,
                (fun childId range providerRun ->
                    LifecycleWorkRecordProjection.lifecycleWorkRecordBoundedForRun
                        scope.Journal
                        childId
                        range
                        providerRun),
                ?journal = scope.Journal,
                onChildCreated =
                    (fun _ _ childId ->
                        scope.SessionParents.[SessionId.value childId] <- SessionId.value managerSessionId),
                onChildCreatedDir =
                    (fun _ childId directory ->
                        directory
                        |> Option.iter (fun path -> scope.RegisterDirectory(SessionId.value childId, path))),
                directoryFor = (fun _ -> scope.DirectoryFor(SessionId.value managerSessionId)),
                onRunStarted = scope.RunStarted,
                parentWorkRecordFor =
                    (fun _ -> LifecycleWorkRecordProjection.lifecycleWorkRecord scope.Journal managerSessionId true),
                childWorkRecordFor = (fun _ -> Task.FromResult None),
                ?sessionSnapshot = scope.Snapshot,
                managerOpensReviewBarrier = false,
                ownership = HandleOwnership.HostOwnedHidden
            )

        let reviewerAgentName = ManagedAgent.nameOf Role.Reviewer

        let openingAssignment () =
            ProviderProse.render (ProviderProse.languageOf managerSessionId) HostReviewPrompt.Opening Map.empty

        let forkReviewerSession (request: FinalityReviewerRequest) : Task<Result<PreparedReviewer, string>> =
            task {
                match!
                    runtime.Fork(
                        request.AgentId,
                        Role.Reviewer,
                        reviewerAgentName,
                        openingAssignment (),
                        None,
                        ownership = HandleOwnership.HostOwnedHidden,
                        deferSend = true
                    )
                with
                | Error error -> return Error error
                | Ok _ ->
                    match runtime.TryChildSession request.AgentId with
                    | Some childId ->
                        return
                            Ok
                                { ReviewerSessionId = childId
                                  IsNew = true }
                    | None -> return Error "reviewer session was not created"
            }

        let prepareSession (request: FinalityReviewerRequest) : Task<Result<PreparedReviewer, string>> =
            task {
                match request.ReviewerSessionId with
                | Some existing ->
                    runtime.AdoptChild(request.AgentId, existing)

                    return
                        Ok
                            { ReviewerSessionId = existing
                              IsNew = false }
                | None -> return! forkReviewerSession request
            }

        let startReview (memberInfo: EnlistedMember) : Task<Result<unit, string>> =
            task {
                if memberInfo.IsNew then
                    return! runtime.SendDeferredFirstPrompt memberInfo.AgentId
                else
                    match!
                        HostSessionNudge.sendContinuation
                            scope.Sessions
                            memberInfo.ReviewerSessionId
                            (openingAssignment ())
                            PromptAuthority.ContinuationKind.ReviewerGuard
                            (scope.DirectoryFor(SessionId.value memberInfo.ReviewerSessionId))
                            scope.Journal
                    with
                    | Error error -> return Error error
                    | Ok _ -> return Ok()
            }

        let awaitTerminal (occasion: ReviewerTerminalOccasion) =
            ReviewerTerminalAwait.awaitFuture scope.Journal scope.Sessions occasion reviewerTimeoutMs

        let sendMissingJudgementNudge
            (durable: AgentJournal)
            (reviewerSessionId: SessionId)
            (barrierId: ReviewBarrierId)
            (terminalProviderRun: ProviderRunIdentity)
            =
            HostSessionNudge.trySendGateContinuationPhysical
                scope.Sessions
                reviewerSessionId
                (ProviderProse.documentFor reviewerSessionId RuntimeNudge.ReviewerVerdictRequired Map.empty)
                PromptAuthority.ContinuationKind.ReviewerGuard
                (scope.DirectoryFor(SessionId.value reviewerSessionId))
                (Some durable)
                (RuntimeNudge.ReviewerVerdictRequired + ":" + ReviewBarrierId.value barrierId)
                terminalProviderRun

        let nudgeMissingJudgement reviewerSessionId barrierId terminalProviderRun =
            match scope.Journal with
            | None -> Task.FromResult(Error "No journal: a Finality reviewer nudge cannot be claimed")
            | Some durable -> sendMissingJudgementNudge durable reviewerSessionId barrierId terminalProviderRun

        let sendRevisionSteer targetSessionId prompt =
            task {
                match!
                    HostSessionNudge.sendContinuation
                        scope.Sessions
                        targetSessionId
                        prompt
                        PromptAuthority.ContinuationKind.FinalitySteer
                        (scope.DirectoryFor(SessionId.value targetSessionId))
                        scope.Journal
                with
                | Ok _ -> return Ok()
                | Error error -> return Error error
            }

        let reviewerPort: FinalityReviewerPort =
            { PrepareSession = prepareSession
              StartReview = startReview
              OpenJudgementChannel = ReviewJudgementInbox.acquire
              AwaitTerminal = awaitTerminal
              NudgeMissingJudgement = nudgeMissingJudgement
              SendRevisionSteer = sendRevisionSteer
              AbortReviewer =
                fun reviewerSessionId ->
                    task {
                        let! _ = scope.Sessions.InterruptAttempt reviewerSessionId
                        return ()
                    }
                    :> Task }

        let readManagerTree port =
            let current = port.GetTreeHash().Trim()

            if String.IsNullOrWhiteSpace current then
                Error "manager Git tree is empty"
            else
                Ok(GitTreeHash.create current)

        let treePort: FinalityTreePort =
            { ReadManagerTree =
                fun sessionId ->
                    try
                        scope.TreePortFor(SessionId.value sessionId)
                        |> Option.map readManagerTree
                        |> Option.defaultValue (Error "manager Git tree is unavailable")
                    with ex ->
                        Error ex.Message }

        reviewerPort, treePort
