namespace Wanxiangshu.Mission.Review.OpenCode

open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

/// judge(verdict) — Reviewer judgment surface. Finality sequencing belongs to
/// ReviewBarrierWorkflow; this tool only emits one typed judgement delivery.
module JudgeTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val Description: string = "tool/judge/description"

        [<Literal>]
        val Received: string = "tool/judge/received"

        [<Literal>]
        val AlreadyJudged: string = "tool/judge/already-judged"

        [<Literal>]
        val NotReceived: string = "tool/judge/not-received"

        [<Literal>]
        val NotFromReviewer: string = "tool/judge/not-from-reviewer"

        [<Literal>]
        val NoActiveIdentity: string = "tool/judge/no-active-identity"

        [<Literal>]
        val VerdictMustBePerfectOrRevise: string = "tool/judge/verdict-must-be-perfect-or-revise"

        [<Literal>]
        val CouldNotBind: string = "tool/judge/could-not-bind"

        [<Literal>]
        val ContextIncomplete: string = "tool/judge/context-incomplete"

        [<Literal>]
        val JudgmentCouldNotBeRecorded: string = "tool/judge/judgment-could-not-be-recorded"

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

    val decideExecution: evidence: ExecutionEvidence -> ExecutionDecision

    val rejectionName: rejection: ExecutionRejection -> string

    val interruptAfterSubmittedJudgement:
        journal: AgentJournal option ->
        cancellation: CancellationToken ->
        currentPhysicalUserMessage: (string -> string option) ->
        runBackground: ((unit -> Task) -> unit) ->
        sessionPort: ISessionHostPort ->
        projectionSessionIdOpt: string option ->
            Task<unit>

    val admission: ToolAdmission

    val spec: factory: HostToolFactory -> scope: ToolRuntimeScope -> ToolSpec
