namespace Wanxiangshu.Mission.Review.OpenCode

open System
open System.Threading
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
        let AlreadyJudged = "tool/judge/already-judged"

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

    let private alreadyJudged ctx =
        ToolHostCodec.tomlObjectWithInstructions [ line ctx Path.AlreadyJudged ] []

    let private challenged ctx =
        ToolHostCodec.tomlObjectWithInstructions [ ProviderProse.render (lang ctx) ReviewChallenge.Path Map.empty ] []

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
        (physicalUserMessageId: PhysicalUserMessageId)
        (submission: VerdictSubmission)
        =
        task {
            match! VerdictWorkflow.recordJudgement journal submission with
            | Error _ -> return notReceived context Path.JudgmentCouldNotBeRecorded
            | Ok() ->
                scope.MarkVerdictSubmitted(context.SessionId, physicalUserMessageId)
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
        | Some submission -> recordSubmittedJudgement scope context journal judgement.PhysicalUserMessageId submission

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
                scope.MarkVerdictSubmitted(context.SessionId, judgement.PhysicalUserMessageId)
                finish (received context)

            let challenge () = finish (challenged context)

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

    [<RequireQualifiedAccess>]
    type private ExecutionDecision =
        | Refused of reasonPath: string
        | AlreadyJudged
        | Proceed of ReviewJudgement

    let private decideExecution
        (scope: ToolRuntimeScope)
        (args: HostToolArguments)
        (context: HostToolContext)
        : ExecutionDecision =
        let isReviewer = scope.RoleFor context = Some Role.Reviewer
        let hasSession = not (String.IsNullOrWhiteSpace context.SessionId)
        let verdict = StaticTools.reviewerVerdictOfString (args.Text "verdict")

        let physicalUserMsg =
            if hasSession then
                scope.CurrentPhysicalUserMessage context.SessionId
            else
                None

        let isSubmitted =
            hasSession
            && (physicalUserMsg
                |> Option.exists (fun messageId ->
                    scope.HasVerdictSubmitted(context.SessionId, PhysicalUserMessageId.create messageId)))

        match
            isReviewer, hasSession, isSubmitted, verdict, context.ToolCallId, context.ProviderRunId, physicalUserMsg
        with
        | false, _, _, _, _, _, _ -> ExecutionDecision.Refused Path.NotFromReviewer
        | _, false, _, _, _, _, _ -> ExecutionDecision.Refused Path.NoActiveIdentity
        | _, _, true, _, _, _, _ -> ExecutionDecision.AlreadyJudged
        | _, _, _, Error _, _, _, _ -> ExecutionDecision.Refused Path.VerdictMustBePerfectOrRevise
        | _, _, _, _, None, _, _
        | _, _, _, _, _, None, _
        | _, _, _, _, _, _, None -> ExecutionDecision.Refused Path.CouldNotBind
        | true, true, false, Ok value, Some toolCallId, Some providerRunId, Some physicalUserMessageId ->
            ExecutionDecision.Proceed
                { ReviewerSessionId = SessionId.create context.SessionId
                  PhysicalUserMessageId = PhysicalUserMessageId.create physicalUserMessageId
                  ProviderRun = providerRunId
                  ToolCallId = toolCallId
                  Verdict = value }

    [<RequireQualifiedAccess>]
    type private SubmittedInterruptDecision =
        | NoInterrupt
        | JournalMissing
        | Ready of journal: AgentJournal * reviewerSessionId: SessionId

    let private decideSubmittedInterrupt
        (journal: AgentJournal option)
        (currentPhysicalUserMessage: string -> string option)
        (projectionSessionIdOpt: string option)
        : SubmittedInterruptDecision =
        let currentRequest =
            projectionSessionIdOpt
            |> Option.bind (fun sessionId ->
                currentPhysicalUserMessage sessionId
                |> Option.map (fun physicalUserMessageId ->
                    SessionId.create sessionId, PhysicalUserMessageId.create physicalUserMessageId))

        let submitted =
            currentRequest
            |> Option.filter (fun (sessionId, physicalUserMessageId) ->
                JudgementRequestIdentity.key sessionId physicalUserMessageId
                |> SharedState.VerdictSubmissions.Contains)

        match submitted, journal with
        | None, _ -> SubmittedInterruptDecision.NoInterrupt
        | Some _, None -> SubmittedInterruptDecision.JournalMissing
        | Some(sessionId, _), Some durable -> SubmittedInterruptDecision.Ready(durable, sessionId)

    let private interruptClosedSubmittedJudgement
        (cancellation: CancellationToken)
        (runBackground: (unit -> Task) -> unit)
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal)
        (reviewerSessionId: SessionId)
        : Task<unit> =
        let scheduleInterrupt () =
            runBackground (fun () ->
                task {
                    match! sessionPort.InterruptAttempt reviewerSessionId with
                    | Ok() -> return ()
                    | Error reason ->
                        return raise (InvalidOperationException("REVIEW_013_TERMINAL_INTERRUPT_FAILED:" + reason))
                }
                :> Task)

        let terminalReadiness () =
            taskResult {
                let! closed =
                    ReviewerWorkflow.ensureSubmittedAttemptClosed journal reviewerSessionId
                    |> TaskResult.mapError (fun reason -> "REVIEW_013_TERMINAL_CLOSURE_FAILED:" + reason)

                if not closed then
                    return false
                else
                    do!
                        ReviewerWorkflow.awaitSubmittedRecordCapture cancellation journal reviewerSessionId
                        |> TaskResult.mapError (fun reason -> "REVIEW_013_RECORD_CAPTURE_FAILED:" + reason)

                    return true
            }

        task {
            match! terminalReadiness () with
            | Ok true ->
                // This hook runs inside the provider request being retired.
                // Awaiting the Host abort here self-locks: the abort endpoint
                // cannot finish until messages.transform returns. Schedule the
                // already-proved physical cleanup under the runtime owner and
                // let this transform return immediately.
                scheduleInterrupt ()
                return ()
            | Ok false -> return ()
            | Error reason -> return invalidOp reason
        }

    let interruptAfterSubmittedJudgement
        (journal: AgentJournal option)
        (cancellation: CancellationToken)
        (currentPhysicalUserMessage: string -> string option)
        (runBackground: (unit -> Task) -> unit)
        (sessionPort: ISessionHostPort)
        (projectionSessionIdOpt: string option)
        : Task<unit> =
        match decideSubmittedInterrupt journal currentPhysicalUserMessage projectionSessionIdOpt with
        | SubmittedInterruptDecision.NoInterrupt -> Task.FromResult()
        | SubmittedInterruptDecision.JournalMissing -> task { return invalidOp "REVIEW_013_TERMINAL_JOURNAL_MISSING" }
        | SubmittedInterruptDecision.Ready(durable, reviewerSessionId) ->
            interruptClosedSubmittedJudgement cancellation runBackground sessionPort durable reviewerSessionId

    let private execute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            match decideExecution scope args context with
            | ExecutionDecision.Refused reason -> return notReceived context reason
            | ExecutionDecision.AlreadyJudged -> return alreadyJudged context
            | ExecutionDecision.Proceed judgement -> return! dispatchJudgement scope context judgement
        }

    let admission: ToolAdmission = fun _ r -> Roles.isAllowed r ToolPermission.Judge

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "judge"
          Description =
            ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) Path.Description Map.empty
          Arguments = [ "verdict", ToolHostCodec.enumSchema [ "PERFECT"; "REVISE" ] factory ]
          Admission = admission
          Execute = execute scope }
