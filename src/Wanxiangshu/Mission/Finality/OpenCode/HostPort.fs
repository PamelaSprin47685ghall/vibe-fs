namespace Wanxiangshu.Mission.Finality.OpenCode

open Wanxiangshu.OpenCode
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation
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
open Wanxiangshu.Mission.Review.Barrier
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
open Wanxiangshu.OpenCode
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Strength

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

        let reviewerAgentName =
            scope.ActiveProfileFor managerSessionId
            |> Option.map (fun profile -> profile.SelectedTier)
            |> Option.defaultValue AgentTier.Deep
            |> fun tier -> ManagedAgent.nameOf tier Role.Reviewer

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
                        runtime.Fork(
                            memberInfo.AgentId,
                            Role.Reviewer,
                            reviewerAgentName,
                            openingAssignment (),
                            None,
                            ownership = HandleOwnership.HostOwnedHidden
                        )
                    with
                    | Error error -> return Error error
                    | Ok _ -> return Ok()
            }

        let awaitTerminal (occasion: ReviewerTerminalOccasion) =
            ReviewerTerminalAwait.awaitFuture scope.Journal scope.Sessions occasion reviewerTimeoutMs

        let sendMissingJudgementNudge (durable: AgentJournal) reviewerSessionId =
            task {
                let acceptedPhysical = ref None
                let dispatcher = PromptDispatcher.forJournal durable

                let! sent =
                    dispatcher.SendAgentOwnerRoot
                        scope.Sessions
                        reviewerSessionId
                        (ProviderProse.documentFor reviewerSessionId RuntimeNudge.ReviewerVerdictRequired Map.empty)
                        reviewerAgentName
                        (scope.DirectoryFor(SessionId.value reviewerSessionId))
                        PromptDispatcher.AwaitMode.Await
                        (Some(fun physical -> acceptedPhysical.Value <- Some physical))

                return
                    match sent, acceptedPhysical.Value with
                    | Error error, _ -> Error error
                    | Ok _, Some physical -> Ok physical
                    | Ok _, None -> Error "Finality reviewer nudge was admitted without a PhysicalUserMessageId"
            }

        let nudgeMissingJudgement reviewerSessionId =
            match scope.Journal with
            | None -> Task.FromResult(Error "No journal: a Finality reviewer nudge cannot be claimed")
            | Some durable -> sendMissingJudgementNudge durable reviewerSessionId

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
