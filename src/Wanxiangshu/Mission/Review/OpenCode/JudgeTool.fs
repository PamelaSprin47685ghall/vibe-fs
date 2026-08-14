namespace Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System
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
open Wanxiangshu.Mission.Finality
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
open Wanxiangshu.Host
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Change
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Mission.Review.Assurance
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Review
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
open Wanxiangshu.Strength
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
