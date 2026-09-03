namespace Wanxiangshu.Mission.Review.OpenCode

open System.Threading
open System
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host.Contract
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Resources

/// Public judgement-tool owner boundary. It exposes the exact parser/schema and
/// contract view; ToolRuntimeScope and HostToolCodec remain implementation-only.
[<RequireQualifiedAccess>]
module JudgeSurface =

    let schemaJson = StaticTools.reviewerVerdictSchemaJson

    let parse (value: string) : obj =
        match StaticTools.reviewerVerdictOfString value with
        | Ok ReviewGuardVerdict.Perfect -> box {| ok = true; value = "Perfect" |}
        | Ok ReviewGuardVerdict.Revise -> box {| ok = true; value = "Revise" |}
        | Error error -> box {| ok = false; error = error |}

    let contract (language: string) : obj =
        let description =
            ProviderResources.readText (ProviderLanguage.parse language) "tool/judge/description"

        box
            {| name = "judge"
               description = description
               arguments =
                [| box
                       {| name = "verdict"
                          values = [| "PERFECT"; "REVISE" |] |} |] |}

    let private optionalIdentity create (value: string) =
        if String.IsNullOrWhiteSpace value then
            None
        else
            Some(create value)

    let private executionDecisionToJs (decision: JudgeTool.ExecutionDecision) : obj =
        match decision with
        | JudgeTool.ExecutionDecision.Refused rejection ->
            box
                {| decision = "Refused"
                   rejection = JudgeTool.rejectionName rejection |}
        | JudgeTool.ExecutionDecision.AlreadyJudged ->
            box
                {| decision = "AlreadyJudged"
                   rejection = "" |}
        | JudgeTool.ExecutionDecision.Proceed judgement ->
            box
                {| decision = "Proceed"
                   rejection = ""
                   sessionId = SessionId.value judgement.ReviewerSessionId
                   physicalUserMessageId = PhysicalUserMessageId.value judgement.PhysicalUserMessageId
                   providerRunId = ProviderRunIdentity.value judgement.ProviderRun
                   toolCallId = ToolCallId.value judgement.ToolCallId
                   verdict =
                    match judgement.Verdict with
                    | ReviewGuardVerdict.Perfect -> "Perfect"
                    | ReviewGuardVerdict.Revise -> "Revise" |}

    let decideExecution
        (role: string)
        (sessionId: string)
        (isSubmitted: bool)
        (verdict: string)
        (toolCallId: string)
        (providerRunId: string)
        (physicalUserMessageId: string)
        : obj =
        let evidence: JudgeTool.ExecutionEvidence =
            { Role = if isNull role then None else Roles.tryParseRole role
              SessionId = sessionId
              IsSubmitted = isSubmitted
              Verdict = StaticTools.reviewerVerdictOfString verdict
              ToolCallId = optionalIdentity ToolCallId.create toolCallId
              ProviderRunId = optionalIdentity ProviderRunIdentity.create providerRunId
              PhysicalUserMessageId =
                if isNull physicalUserMessageId then
                    None
                else
                    Some physicalUserMessageId }

        evidence |> JudgeTool.decideExecution |> executionDecisionToJs

    let receipt (language: string) =
        ProviderResources.readText (ProviderLanguage.parse language) "tool/judge/received"

    let alreadyJudged (language: string) =
        ProviderResources.readText (ProviderLanguage.parse language) "tool/judge/already-judged"

    let markVerdictSubmitted (sessionId: string) (physicalUserMessageId: string) : unit =
        let reviewer = SessionId.create sessionId

        SharedState.VerdictSubmissions
        |> Seq.filter (JudgementRequestIdentity.belongsTo reviewer)
        |> Seq.toArray
        |> Array.iter (fun request -> SharedState.VerdictSubmissions.Remove request |> ignore)

        JudgementRequestIdentity.key reviewer (PhysicalUserMessageId.create physicalUserMessageId)
        |> SharedState.VerdictSubmissions.Add
        |> ignore

    let hasVerdictSubmitted (sessionId: string) (physicalUserMessageId: string) : bool =
        JudgementRequestIdentity.key (SessionId.create sessionId) (PhysicalUserMessageId.create physicalUserMessageId)
        |> SharedState.VerdictSubmissions.Contains

    let clearVerdictSubmissions () : unit = SharedState.VerdictSubmissions.Clear()

    type private PhysicalPort(raw: obj) =
        let unavailable () =
            Task.FromResult(Error "JudgeSurface physical probe supports only InterruptAttempt")

        interface ISessionHostPort with
            member _.SubscribeTerminal(_, _) =
                { new IDisposable with
                    member _.Dispose() = () }

            member _.SubscribeFutureTerminal(_, _) =
                { new IDisposable with
                    member _.Dispose() = () }

            member _.SendPrompt(_, _, _) =
                Task.FromResult(Wanxiangshu.Foundation.Outcome.SendOutcome.Fatal "JudgeSurface physical probe")

            member _.AbortSession(_) = unavailable ()

            member _.InterruptAttempt(sessionId) =
                task {
                    let! _ = Fable.Core.JsInterop.emitJsExpr (raw, SessionId.value sessionId) "$0.InterruptAttempt($1)"

                    return Ok()
                }

            member _.IsManagedChild(_) = false
            member _.AbortChildren(_) = Task.FromResult()
            member _.CreateSiblingSession(_, _, _) = unavailable ()
            member _.TryGetParentSession(_) = unavailable ()
            member _.CreateChildSession(_, _) = unavailable ()
            member _.ListChildren(_) = unavailable ()
            member _.FamilyRootOf(sessionId) = sessionId

    let interruptAfterSubmittedJudgement
        (handle: JournalHandle)
        (physicalUserMessageId: string)
        (runBackground: obj)
        (sessionPort: obj)
        (sessionId: string)
        : Task<obj> =
        task {
            let currentPhysicalUserMessage candidate =
                if candidate = sessionId then
                    Some physicalUserMessageId
                else
                    None

            let schedule work =
                Fable.Core.JsInterop.emitJsExpr (runBackground, work) "$0($1)" |> ignore

            try
                do!
                    JudgeTool.interruptAfterSubmittedJudgement
                        (Some handle.Journal)
                        CancellationToken.None
                        currentPhysicalUserMessage
                        schedule
                        (PhysicalPort(sessionPort) :> ISessionHostPort)
                        (Some sessionId)

                return box {| ok = true; error = "" |}
            with ex ->
                return box {| ok = false; error = ex.Message |}
        }
