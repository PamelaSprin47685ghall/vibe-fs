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
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal

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
        ProviderLanguageBinding.forSessionText ctx.SessionId

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
            Task.FromResult(notReceived context Path.CouldNotBind)

    [<RequireQualifiedAccess>]
    type ExecutionRejection =
        | NotFromReviewer
        | NoActiveIdentity
        | VerdictMustBePerfectOrRevise
        | CouldNotBind

    [<RequireQualifiedAccess>]
    type ExecutionDecision =
        | Refused of ExecutionRejection
        | AlreadyJudged
        | Proceed of ReviewJudgement

    type ExecutionEvidence =
        { Role: Role option
          SessionId: string
          IsSubmitted: bool
          Verdict: Result<ReviewGuardVerdict, string>
          ToolCallId: ToolCallId option
          ProviderRunId: ProviderRunIdentity option
          PhysicalUserMessageId: string option }

    let decideExecution (evidence: ExecutionEvidence) : ExecutionDecision =
        let physicalUserMessageId =
            evidence.PhysicalUserMessageId
            |> Option.map PhysicalUserMessageId.create
            |> Option.filter PhysicalUserMessageId.isNonBlank

        let currentRequestSubmitted =
            evidence.IsSubmitted && Option.isSome physicalUserMessageId

        match
            evidence.Role = Some Role.Reviewer,
            String.IsNullOrWhiteSpace evidence.SessionId,
            currentRequestSubmitted,
            evidence.Verdict,
            evidence.ToolCallId,
            evidence.ProviderRunId,
            physicalUserMessageId
        with
        | false, _, _, _, _, _, _ -> ExecutionDecision.Refused ExecutionRejection.NotFromReviewer
        | _, true, _, _, _, _, _ -> ExecutionDecision.Refused ExecutionRejection.NoActiveIdentity
        | _, _, true, _, _, _, _ -> ExecutionDecision.AlreadyJudged
        | _, _, false, Error _, _, _, _ -> ExecutionDecision.Refused ExecutionRejection.VerdictMustBePerfectOrRevise
        | _, _, false, Ok _, None, _, _
        | _, _, false, Ok _, _, None, _
        | _, _, false, Ok _, _, _, None -> ExecutionDecision.Refused ExecutionRejection.CouldNotBind
        | true, false, false, Ok verdict, Some toolCallId, Some providerRunId, Some physical ->
            ExecutionDecision.Proceed
                { ReviewerSessionId = SessionId.create evidence.SessionId
                  PhysicalUserMessageId = physical
                  ProviderRun = providerRunId
                  ToolCallId = toolCallId
                  Verdict = verdict }

    let rejectionPath (rejection: ExecutionRejection) =
        match rejection with
        | ExecutionRejection.NotFromReviewer -> Path.NotFromReviewer
        | ExecutionRejection.NoActiveIdentity -> Path.NoActiveIdentity
        | ExecutionRejection.VerdictMustBePerfectOrRevise -> Path.VerdictMustBePerfectOrRevise
        | ExecutionRejection.CouldNotBind -> Path.CouldNotBind

    let rejectionName (rejection: ExecutionRejection) =
        match rejection with
        | ExecutionRejection.NotFromReviewer -> "NotFromReviewer"
        | ExecutionRejection.NoActiveIdentity -> "NoActiveIdentity"
        | ExecutionRejection.VerdictMustBePerfectOrRevise -> "VerdictMustBePerfectOrRevise"
        | ExecutionRejection.CouldNotBind -> "CouldNotBind"

    let private executionEvidence
        (scope: ToolRuntimeScope)
        (args: HostToolArguments)
        (context: HostToolContext)
        : ExecutionEvidence =
        let physicalUserMessageId =
            if String.IsNullOrWhiteSpace context.SessionId then
                None
            else
                scope.CurrentPhysicalUserMessage context.SessionId

        { Role = scope.RoleFor context
          SessionId = context.SessionId
          IsSubmitted =
            physicalUserMessageId
            |> Option.exists (fun value ->
                scope.HasVerdictSubmitted(context.SessionId, PhysicalUserMessageId.create value))
          Verdict = StaticTools.reviewerVerdictOfString (args.Text "verdict")
          ToolCallId = context.ToolCallId
          ProviderRunId = context.ProviderRunId
          PhysicalUserMessageId = physicalUserMessageId }

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
                |> Option.map PhysicalUserMessageId.create
                |> Option.filter PhysicalUserMessageId.isNonBlank
                |> Option.map (fun physicalUserMessageId -> SessionId.create sessionId, physicalUserMessageId))

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
                    let! _ =
                        task {
                            try
                                return! sessionPort.InterruptAttempt reviewerSessionId
                            with ex ->
                                return Error ex.Message
                        }

                    return ()
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
            match executionEvidence scope args context |> decideExecution with
            | ExecutionDecision.Refused rejection -> return notReceived context (rejectionPath rejection)
            | ExecutionDecision.AlreadyJudged -> return alreadyJudged context
            | ExecutionDecision.Proceed judgement -> return! dispatchJudgement scope context judgement
        }

    let admission: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ r -> OfficeCapability.isAllowed r ToolPermission.Judge)

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "judge"
          Description =
            ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) Path.Description Map.empty
          Arguments = [ "verdict", ToolHostCodec.enumSchema [ "PERFECT"; "REVISE" ] factory ]
          Admission = admission
          Execute = execute scope }
