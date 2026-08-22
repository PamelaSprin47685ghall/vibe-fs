namespace Wanxiangshu.Mission.Review.OpenCode

open System.Threading
open System
open System.Threading.Tasks
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

    /// The public fail-closed precedence used by JudgeTool before any identity
    /// or tree lookup. This is diagnostic text, not internal state.
    let validateContext
        (role: string)
        (sessionId: string)
        (hasOwner: bool)
        (hasParent: bool)
        (hasBarrier: bool)
        (hasTree: bool)
        : obj =
        if role <> "Reviewer" then
            box
                {| ok = false
                   message = "This verdict did not come from a Reviewer session." |}
        elif System.String.IsNullOrWhiteSpace sessionId then
            box
                {| ok = false
                   message = "judgment authority is established before review context" |}
        elif not hasOwner || not hasParent || not hasBarrier || not hasTree then
            box
                {| ok = false
                   message = "review context is incomplete" |}
        else
            box {| ok = true; message = "" |}

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

    let private tryGetJournal (handle: JournalHandle) : Result<AgentJournal, string> =
        try
            Ok handle.Journal
        with ex ->
            Error ex.Message

    let ensureSubmittedAttemptClosed (handle: JournalHandle) (sessionId: string) : Task<obj> =
        task {
            let! result =
                match tryGetJournal handle with
                | Error reason -> Task.FromResult(Error reason)
                | Ok journal -> ReviewerWorkflow.ensureSubmittedAttemptClosed journal (SessionId.create sessionId)

            match result with
            | Ok closed -> return box {| ok = true; closed = closed |}
            | Error reason -> return box {| ok = false; error = reason |}
        }

    let interruptAfterSubmittedJudgement
        (handle: JournalHandle)
        (physicalUserMessageId: string)
        (sessionPort: obj)
        (sessionId: string)
        : Task<obj> =
        task {
            let reviewer = SessionId.create sessionId

            let isSubmitted =
                JudgementRequestIdentity.key reviewer (PhysicalUserMessageId.create physicalUserMessageId)
                |> SharedState.VerdictSubmissions.Contains

            let! closedResult =
                match isSubmitted, tryGetJournal handle with
                | false, _ -> Task.FromResult(Ok false)
                | true, Error reason -> Task.FromResult(Error reason)
                | true, Ok journal -> ReviewerWorkflow.ensureSubmittedAttemptClosed journal reviewer

            match closedResult with
            | Ok true ->
                match! ReviewerWorkflow.awaitSubmittedRecordCapture CancellationToken.None handle.Journal reviewer with
                | Error reason ->
                    return
                        box
                            {| ok = false
                               error = "REVIEW_013_RECORD_CAPTURE_FAILED:" + reason
                               interrupted = false |}
                | Ok() ->
                    let fn = Fable.Core.JsInterop.emitJsExpr (sessionPort, "InterruptAttempt") "$0[$1]"

                    let! _ =
                        if isNull fn then
                            Task.FromResult(Ok())
                        else
                            Fable.Core.JsInterop.emitJsExpr (sessionPort, sessionId) "$0.InterruptAttempt($1)"

                    return box {| ok = true; interrupted = true |}
            | Ok false -> return box {| ok = true; interrupted = false |}
            | Error reason ->
                return
                    box
                        {| ok = false
                           error = "REVIEW_013_TERMINAL_CLOSURE_FAILED:" + reason
                           interrupted = false |}
        }
