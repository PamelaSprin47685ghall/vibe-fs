namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Finality
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

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
                    (fun _ -> XTraceCapture.lifecycleWorkRecord scope.Journal managerSessionId true),
                childWorkRecordFor = (fun _ -> None),
                ?sessionSnapshot = scope.Snapshot,
                managerOpensReviewBarrier = false,
                ownership = HandleOwnership.HostOwnedHidden
            )

        let reviewerAgentName =
            scope.ActiveProfileFor managerSessionId
            |> Option.map (fun profile -> profile.SelectedTier)
            |> Option.defaultValue AgentTier.Deep
            |> fun tier -> ManagedAgent.nameOf tier Role.Reviewer

        let prepareSession (request: FinalityReviewerRequest) =
            task {
                match request.ReviewerSessionId with
                | Some existing ->
                    runtime.AdoptChild(request.AgentId, existing)
                    return Ok { ReviewerSessionId = existing; IsNew = false }
                | None ->
                    match!
                        runtime.Fork(
                            request.AgentId,
                            Role.Reviewer,
                            reviewerAgentName,
                            HostReviewPrompt.OpeningAssignment,
                            None,
                            ownership = HandleOwnership.HostOwnedHidden,
                            deferSend = true
                        )
                    with
                    | Error error -> return Error error
                    | Ok _ ->
                        match runtime.TryChildSession request.AgentId with
                        | None -> return Error "reviewer session was not created"
                        | Some childId -> return Ok { ReviewerSessionId = childId; IsNew = true }
            }

        let startReview (memberInfo: EnlistedMember) =
            task {
                if memberInfo.IsNew then
                    return! runtime.SendDeferredFirstPrompt memberInfo.AgentId
                else
                    match!
                        runtime.Fork(
                            memberInfo.AgentId,
                            Role.Reviewer,
                            reviewerAgentName,
                            HostReviewPrompt.OpeningAssignment,
                            None
                        )
                    with
                    | Error error -> return Error error
                    | Ok _ -> return Ok()
            }

        let awaitTerminal reviewerSessionId =
            task {
                let completed =
                    TaskCompletionSource<TerminalOutcome>(TaskCreationOptions.RunContinuationsAsynchronously)

                let accepting = ref false

                use subscription =
                    scope.Sessions.SubscribeTerminal(
                        reviewerSessionId,
                        fun _ outcome ->
                            if accepting.Value then
                                AsyncSupport.trySetResult completed outcome |> ignore
                    )

                accepting.Value <- true

                let finished =
                    task {
                        match! completed.Task with
                        | TerminalOutcome.Completed _ -> return Ok()
                        | TerminalOutcome.Failed error -> return Error error
                        | TerminalOutcome.Aborted reason -> return Error reason
                    }

                let timedOut: Task<Result<unit, string>> =
                    emitJsExpr
                        reviewerTimeoutMs
                        "new Promise(function (resolve) { var t = setTimeout(function () { resolve({ tag: 1, fields: ['await reviewer timed out'] }); }, $0); if (t && typeof t.unref === 'function') t.unref(); })"

                return!
                    emitJsExpr (finished, timedOut) "Promise.race([$0, $1])": Task<Result<unit, string>>
            }

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
              AwaitTerminal = awaitTerminal
              SendRevisionSteer = sendRevisionSteer
              AbortReviewer = fun reviewerSessionId -> scope.Sessions.AbortSession reviewerSessionId |> ignore }

        let treePort: FinalityTreePort =
            { ReadManagerTree =
                fun sessionId ->
                    try
                        match scope.TreePortFor(SessionId.value sessionId) with
                        | None -> Error "manager Git tree is unavailable"
                        | Some port ->
                            let current = port.GetTreeHash().Trim()

                            if String.IsNullOrWhiteSpace current then
                                Error "manager Git tree is empty"
                            else
                                Ok(GitTreeHash.create current)
                    with ex ->
                        Error ex.Message }

        reviewerPort, treePort
