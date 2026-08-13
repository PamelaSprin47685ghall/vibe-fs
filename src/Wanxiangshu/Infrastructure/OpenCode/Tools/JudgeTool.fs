namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Resources
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

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Description = "tool/judge/description"

        [<Literal>]
        let Received = "tool/judge/received"

        [<Literal>]
        let NotReceived = "tool/judge/not-received"

        [<Literal>]
        let ChallengeUnproven = "tool/judge/challenge-unproven"

        [<Literal>]
        let NotFromReviewer = "tool/judge/not-from-reviewer"

        [<Literal>]
        let NoActiveIdentity = "tool/judge/no-active-identity"

        [<Literal>]
        let VerdictMustBePerfectOrRevise = "tool/judge/verdict-must-be-perfect-or-revise"

        [<Literal>]
        let CouldNotBind = "tool/judge/could-not-bind"

        [<Literal>]
        let ContextIncomplete = "tool/judge/context-incomplete"

        [<Literal>]
        let ChallengeProofNotRecorded = "tool/judge/challenge-proof-not-recorded"

        [<Literal>]
        let JudgmentCouldNotBeRecorded = "tool/judge/judgment-could-not-be-recorded"

    let private lang (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private line (ctx: HostToolContext) path =
        ProviderProse.render (lang ctx) path Map.empty

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

    let private received ctx =
        ToolHostCodec.tomlObjectWithInstructions [ line ctx Path.Received ] []

    let private notReceived ctx reasonPath =
        ToolHostCodec.tomlObjectWithInstructions [ line ctx Path.NotReceived; line ctx reasonPath ] []

    let private challengeUnproven ctx = notReceived ctx Path.ChallengeUnproven

    let private report ctx (decision: VerdictDecision) =
        match decision with
        | VerdictDecision.Revised -> received ctx
        | VerdictDecision.ChallengeIssued challenge ->
            let instructions =
                if String.IsNullOrWhiteSpace challenge then
                    [ line ctx Path.Received ]
                else
                    [ line ctx Path.Received; challenge ]

            ToolHostCodec.tomlObjectWithInstructions instructions []
        | VerdictDecision.Confirmed -> received ctx
        | VerdictDecision.ChallengeUnproven -> challengeUnproven ctx
        | VerdictDecision.AlreadyCounted -> received ctx
        | VerdictDecision.ProcessTerminal -> received ctx

    let private execute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            let verdict = StaticTools.reviewerVerdictOfString (args.Text "verdict")

            let validated =
                if scope.RoleFor context <> Some Role.Reviewer then
                    Error Path.NotFromReviewer
                elif String.IsNullOrWhiteSpace context.SessionId then
                    Error Path.NoActiveIdentity
                else
                    match verdict, context.ToolCallId, context.ProviderRunId with
                    | Error _, _, _ -> Error Path.VerdictMustBePerfectOrRevise
                    | _, None, _
                    | _, _, None -> Error Path.CouldNotBind
                    | Ok value, Some toolCallId, Some providerRunId -> Ok(value, toolCallId, providerRunId)

            match validated with
            | Error reason -> return notReceived context reason
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
                | _, _, _, None -> return notReceived context Path.ContextIncomplete
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

                    match! VerdictWorkflow.submit journal HostDigest.sha256Hex submission with
                    | Ok VerdictDecision.ChallengeUnproven ->
                        match!
                            ReviewSeal.bindToRun
                                journal
                                scope.PendingReviewSeals
                                (SessionId.create reviewerId)
                                providerRunId
                        with
                        | Error ReviewSeal.NoPendingSeal -> return challengeUnproven context
                        | Error(ReviewSeal.AppendFailed _) -> return notReceived context Path.ChallengeProofNotRecorded
                        | Ok _ ->
                            match! VerdictWorkflow.submit journal HostDigest.sha256Hex submission with
                            | Error _ -> return notReceived context Path.JudgmentCouldNotBeRecorded
                            | Ok VerdictDecision.ChallengeUnproven -> return challengeUnproven context
                            | Ok decision ->
                                scope.PendingReviewSeals.Remove reviewerId |> ignore
                                scope.MarkVerdictSubmitted reviewerId
                                return report context decision
                    | Ok decision ->
                        scope.MarkVerdictSubmitted reviewerId
                        return report context decision
                    | Error _ -> return notReceived context Path.JudgmentCouldNotBeRecorded
        }

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "judge"
          Description =
            ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) Path.Description Map.empty
          Arguments = [ "verdict", ToolHostCodec.enumSchema [ "PERFECT"; "REVISE" ] factory ]
          Execute = execute scope }
