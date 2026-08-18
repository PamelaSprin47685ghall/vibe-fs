namespace Wanxiangshu.Mission.Review.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Resources

/// judge(verdict) — Reviewer judgment surface. Finality sequencing belongs to
/// ReviewBarrierWorkflow; this tool only emits one typed judgement delivery.
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
        let JudgmentCouldNotBeRecorded = "tool/judge/judgment-could-not-be-recorded"

    let private lang (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private line (ctx: HostToolContext) path =
        ProviderProse.render (lang ctx) path Map.empty

    let private received ctx =
        ToolHostCodec.tomlObjectWithInstructions [ line ctx Path.Received ] []

    let private challenged ctx =
        ToolHostCodec.tomlObjectWithInstructions
            [ line ctx Path.Received
              ProviderProse.render (lang ctx) ReviewChallenge.Path Map.empty ]
            []

    let private notReceived ctx reasonPath =
        ToolHostCodec.tomlObjectWithInstructions [ line ctx Path.NotReceived; line ctx reasonPath ] []

    let private processSubmission (journal: AgentJournal) (judgement: ReviewJudgement) : VerdictSubmission option =
        let snapshot = AgentJournal.snapshot journal

        MagicTodoProjection.pendingProcessReviewForReviewer
            judgement.ReviewerSessionId
            snapshot.AgentProjections.MagicTodo
        |> Option.bind (fun checkpoint ->
            MagicTodoProjection.assignment checkpoint
            |> Option.bind (fun assignment ->
                AgentProjection.tryFind judgement.ReviewerSessionId snapshot.AgentProjections
                |> Option.bind (fun session -> session.ReviewGuard)
                |> Option.bind (fun guard ->
                    guard.LastGitTreeHash
                    |> Option.map (fun tree ->
                        { BarrierId = ReviewBarrierId.create (MagicTodo.TodoReviewId.value assignment.TodoReviewId)
                          GitTreeHash = tree
                          ManagerSessionId = checkpoint.ManagerSessionId
                          ReviewerSessionId = judgement.ReviewerSessionId
                          ProviderRun = judgement.ProviderRun
                          ToolCallId = judgement.ToolCallId
                          Verdict = judgement.Verdict }))))

    let private recordSubmittedJudgement
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (journal: AgentJournal)
        (submission: VerdictSubmission)
        =
        task {
            match! VerdictWorkflow.recordJudgement journal submission with
            | Error _ -> return notReceived context Path.JudgmentCouldNotBeRecorded
            | Ok() ->
                scope.MarkVerdictSubmitted context.SessionId
                return received context
        }

    let private recordJournalJudgement
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (journal: AgentJournal)
        (judgement: ReviewJudgement)
        =
        match processSubmission journal judgement with
        | None -> Task.FromResult(notReceived context Path.ContextIncomplete)
        | Some submission -> recordSubmittedJudgement scope context journal submission

    let private recordProcessJudgement
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (judgement: ReviewJudgement)
        =
        match scope.Journal with
        | None -> Task.FromResult(notReceived context Path.ContextIncomplete)
        | Some journal -> recordJournalJudgement scope context journal judgement

    let private deliverFinalityJudgement
        (scope: ToolRuntimeScope)
        (context: HostToolContext)
        (judgement: ReviewJudgement)
        =
        task {
            let completed =
                TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously)

            let finish value =
                AsyncSupport.trySetResult completed value |> ignore

            let accept () =
                scope.MarkVerdictSubmitted context.SessionId
                finish (received context)

            let challenge () =
                scope.MarkVerdictSubmitted context.SessionId
                finish (challenged context)

            let reject () =
                finish (notReceived context Path.JudgmentCouldNotBeRecorded)

            match ReviewJudgementInbox.tryDeliver judgement accept challenge reject with
            | None -> return notReceived context Path.CouldNotBind
            | Some() -> return! completed.Task
        }

    let private dispatchJudgement (scope: ToolRuntimeScope) (context: HostToolContext) (judgement: ReviewJudgement) =
        if ReviewJudgementInbox.isOwned judgement.ReviewerSessionId then
            deliverFinalityJudgement scope context judgement
        else
            recordProcessJudgement scope context judgement

    let private execute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            let verdict = StaticTools.reviewerVerdictOfString (args.Text "verdict")

            let validated =
                if scope.RoleFor context <> Some Role.Reviewer then
                    Error Path.NotFromReviewer
                elif String.IsNullOrWhiteSpace context.SessionId then
                    Error Path.NoActiveIdentity
                else
                    match
                        verdict,
                        context.ToolCallId,
                        context.ProviderRunId,
                        scope.CurrentPhysicalUserMessage context.SessionId
                    with
                    | Error _, _, _, _ -> Error Path.VerdictMustBePerfectOrRevise
                    | _, None, _, _
                    | _, _, None, _
                    | _, _, _, None -> Error Path.CouldNotBind
                    | Ok value, Some toolCallId, Some providerRunId, Some physicalUserMessageId ->
                        Ok
                            { ReviewerSessionId = SessionId.create context.SessionId
                              PhysicalUserMessageId = PhysicalUserMessageId.create physicalUserMessageId
                              ProviderRun = providerRunId
                              ToolCallId = toolCallId
                              Verdict = value }

            match validated with
            | Error reason -> return notReceived context reason
            | Ok judgement -> return! dispatchJudgement scope context judgement
        }

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "judge"
          Description =
            ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) Path.Description Map.empty
          Arguments = [ "verdict", ToolHostCodec.enumSchema [ "PERFECT"; "REVISE" ] factory ]
          Execute = execute scope }
