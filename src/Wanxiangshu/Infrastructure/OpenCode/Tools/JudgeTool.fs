namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Review
open Wanxiangshu.Session
open Wanxiangshu.Tools

/// judge(verdict) — Reviewer judgment surface. Durable identity/witness details
/// stay internal; provider-visible outcomes are natural review consequences.
module JudgeTool =

    let private reviewOwner (scope: ToolRuntimeScope) (reviewerId: string) =
        match scope.SessionParents.TryGetValue reviewerId with
        | true, parentId -> Some(SessionId.create parentId)
        | false, _ -> None

    let private jobIdentity (scope: ToolRuntimeScope) (managerSessionId: SessionId) =
        match scope.Journal with
        | None -> None, None
        | Some journal ->
            match
                OrchestratorProjection.tryFindByManagerSession
                    managerSessionId
                    (AgentJournal.snapshot journal).AgentProjections.Orchestrator
            with
            | None -> None, None
            | Some job -> Some job.ManagerJobId, Some job.WorktreeIdentity

    let private currentBarrier (scope: ToolRuntimeScope) (reviewerId: string) =
        match scope.Journal with
        | None -> None
        | Some journal ->
            AgentProjection.tryFind (SessionId.create reviewerId) (AgentJournal.snapshot journal).AgentProjections
            |> Option.bind (fun session -> session.ReviewGuard)
            |> Option.bind (fun guard -> guard.CurrentBarrierId)

    let private received () =
        ToolHostCodec.tomlObjectWithInstructions [ "# Your judgment has been received." ] []

    let private notReceived reason =
        ToolHostCodec.tomlObjectWithInstructions
            [ "# Your judgment was not received."
              "# " + reason ]
            []

    let private challengeUnproven () =
        notReceived "The previous challenge is not proven to have reached this review turn."

    let private report (decision: VerdictDecision) =
        match decision with
        | VerdictDecision.Revised -> received ()
        | VerdictDecision.ChallengeIssued challenge ->
            let instructions =
                if String.IsNullOrWhiteSpace challenge then
                    [ "# Your judgment has been received." ]
                else
                    [ "# Your judgment has been received."; challenge ]

            ToolHostCodec.tomlObjectWithInstructions instructions []
        | VerdictDecision.Confirmed -> received ()
        | VerdictDecision.ChallengeUnproven -> challengeUnproven ()
        | VerdictDecision.AlreadyCounted -> received ()

    let private execute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            let verdict = StaticTools.reviewerVerdictOfString (args.Text "verdict")

            let validated =
                if scope.RoleFor context <> Some Role.Reviewer then
                    Error "This judgment did not come from a Reviewer."
                elif String.IsNullOrWhiteSpace context.SessionId then
                    Error "This review turn has no active identity."
                else
                    match verdict, context.ToolCallId, context.ProviderRunId with
                    | Error _, _, _ -> Error "The verdict must be PERFECT or REVISE."
                    | _, None, _
                    | _, _, None -> Error "This judgment could not be bound to the current review turn."
                    | Ok value, Some toolCallId, Some providerRunId -> Ok(value, toolCallId, providerRunId)

            match validated with
            | Error reason -> return notReceived reason
            | Ok(value, toolCallId, providerRunId) ->
                let reviewerId = context.SessionId

                match
                    scope.Journal,
                    reviewOwner scope reviewerId,
                    scope.TreePortFor reviewerId,
                    currentBarrier scope reviewerId
                with
                | None, _, _, _
                | _, None, _, _
                | _, _, None, _
                | _, _, _, None ->
                    return notReceived "The review context is incomplete, so no judgment was recorded."
                | Some journal, Some managerSessionId, Some gitTree, Some barrierId ->
                    let managerJobId, worktreeIdentity = jobIdentity scope managerSessionId

                    let submission: VerdictSubmission =
                        { BarrierId = barrierId
                          GitTreeHash = GitTreeHash.create (gitTree.GetTreeHash())
                          ManagerSessionId = managerSessionId
                          ReviewerSessionId = SessionId.create reviewerId
                          ManagerJobId = managerJobId
                          WorktreeIdentity = worktreeIdentity
                          ProviderRun = providerRunId
                          ToolCallId = toolCallId
                          Verdict = value }

                    match VerdictWorkflow.submit journal HostDigest.sha256Hex submission with
                    | Ok VerdictDecision.ChallengeUnproven ->
                        match
                            ReviewSeal.bindToRun
                                journal
                                scope.PendingReviewSeals
                                (SessionId.create reviewerId)
                                providerRunId
                        with
                        | Error ReviewSeal.NoPendingSeal -> return challengeUnproven ()
                        | Error(ReviewSeal.AppendFailed _) ->
                            return notReceived "The challenge proof could not be recorded for this turn."
                        | Ok _ ->
                            match VerdictWorkflow.submit journal HostDigest.sha256Hex submission with
                            | Error _ -> return notReceived "The judgment could not be recorded."
                            | Ok VerdictDecision.ChallengeUnproven -> return challengeUnproven ()
                            | Ok decision ->
                                scope.PendingReviewSeals.Remove reviewerId |> ignore
                                scope.MarkVerdictSubmitted reviewerId
                                return report decision
                    | Ok decision ->
                        scope.MarkVerdictSubmitted reviewerId
                        return report decision
                    | Error _ -> return notReceived "The judgment could not be recorded."
        }

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "judge"
          Description = "Speak your review judgment"
          Arguments = [ "verdict", ToolHostCodec.enumSchema [ "PERFECT"; "REVISE" ] factory ]
          Execute = execute scope }
