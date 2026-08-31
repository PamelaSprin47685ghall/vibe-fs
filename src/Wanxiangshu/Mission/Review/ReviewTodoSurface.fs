namespace Wanxiangshu.Mission.Review

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoProjection
open Wanxiangshu.Persistence.Journal

/// Review-owned Magic Todo surface for integration tests.
[<RequireQualifiedAccess>]
module ReviewTodoSurface =

    let private text (value: obj) =
        if isNull value then "" else string value

    let private field (value: obj) (name: string) : obj =
        if isNull value then
            null
        else
            emitJsExpr (value, name) "$0[$1]"

    let private strField value name = text (field value name)

    let private intField value name = unbox<int> (field value name)

    let private boolField value name = unbox<bool> (field value name)

    let private cursorOf value =
        let nested = field value "Sequence"

        let sequence =
            if isNull nested then
                int64 (unbox<int> value)
            else
                int64 (unbox<int> nested)

        XTraceCursor.create sequence

    let private blobRefOf value = BlobRef.create (text value)
    let private blobDigestOf value = BlobDigest.create (text value)
    let private lifeOf value = ManagerLifeId.create (text value)
    let private writeOf value = TodoWriteId.create (text value)
    let private sessionOf value = SessionId.create (text value)
    let private callOf value = ToolCallId.create (text value)

    let private physicalEvidenceResult value : Result<PhysicalSuccessEvidence, string> =
        match text value with
        | "RecoveredCompletedToolPart" -> Ok PhysicalSuccessEvidence.RecoveredCompletedToolPart
        | "LiveAfterSuccess" -> Ok PhysicalSuccessEvidence.LiveAfterSuccess
        | unknown -> Error(sprintf "unknown physical success evidence: %s" unknown)

    let private physicalEvidenceOf value =
        match physicalEvidenceResult value with
        | Ok evidence -> evidence
        | Error error -> invalidArg "PhysicalSuccessEvidence" error

    let private factOf (caseName: string) (payload: obj) : MagicTodoFact =
        match caseName with
        | "TodoWritePrepared" ->
            let prepared: TodoWritePrepared =
                { ManagerSessionId = sessionOf (field payload "ManagerSessionId")
                  ManagerLifeId = lifeOf (field payload "ManagerLifeId")
                  TodoWriteId = writeOf (field payload "TodoWriteId")
                  ToolCallId = callOf (field payload "ToolCallId")
                  ToolPartOrdinal = intField payload "ToolPartOrdinal"
                  BaseTodoRef = blobRefOf (field payload "BaseTodoRef")
                  BaseTodoDigest = blobDigestOf (field payload "BaseTodoDigest")
                  ProposedTodoRef = blobRefOf (field payload "ProposedTodoRef")
                  ProposedTodoDigest = blobDigestOf (field payload "ProposedTodoDigest")
                  PlanCompleteDeclared = boolField payload "PlanCompleteDeclared"
                  ProviderInputDigest = strField payload "ProviderInputDigest"
                  ReviewFrontier = cursorOf (field payload "ReviewFrontier")
                  SemanticVersion = strField payload "SemanticVersion" }

            MagicTodoFact.TodoWritePrepared prepared
        | "TodoWriteAccepted" ->
            let accepted: TodoWriteAccepted =
                { ManagerLifeId = lifeOf (field payload "ManagerLifeId")
                  TodoWriteId = writeOf (field payload "TodoWriteId")
                  ToolCallId = callOf (field payload "ToolCallId")
                  PreparedFactRef = EventId.create (strField payload "PreparedFactRef")
                  InputDigest = strField payload "InputDigest"
                  OutputDigest = strField payload "OutputDigest"
                  PhysicalSuccessEvidence = physicalEvidenceOf (field payload "PhysicalSuccessEvidence")
                  SemanticVersion = strField payload "SemanticVersion" }

            MagicTodoFact.TodoWriteAccepted accepted
        | other -> failwith $"ReviewTodoSurface: unknown Magic Todo fact '{other}'"

    let private factValidation (caseName: string) (payload: obj) : Result<unit, string> =
        match caseName with
        | "TodoWriteAccepted" ->
            physicalEvidenceResult (field payload "PhysicalSuccessEvidence")
            |> Result.map (fun _ -> ())
        | _ -> Ok()

    let private encodedFact (caseName: string) (payload: obj) : string =
        match factValidation caseName payload with
        | Error error -> invalidArg "payload" error
        | Ok() -> MagicTodoFactCodec.encode (factOf caseName payload)

    let factJson (caseName: string) (payload: obj) : obj =
        match factValidation caseName payload with
        | Error error -> box {| ok = false; error = error |}
        | Ok() -> box (encodedFact caseName payload)

    let ids (sha256: obj) (lifeId: string) (callId: string) : obj =
        let hash (value: string) = emitJsExpr (sha256, value) "$0($1)"
        let life = ManagerLifeId.create lifeId
        let call = ToolCallId.create callId
        let write = MagicTodo.todoWriteId hash life call

        box {| todoWriteId = TodoWriteId.value write |}

    let newProjection () = MagicTodoProjectionSurface.create ()

    let fold (projection: MagicTodoProjectionHandle) (eventId: string) (caseName: string) (payload: obj) : obj =
        MagicTodoProjectionSurface.fold projection eventId (encodedFact caseName payload)

    let view (projection: MagicTodoProjectionHandle) (lifeId: string) : obj =
        MagicTodoProjectionSurface.view projection lifeId
