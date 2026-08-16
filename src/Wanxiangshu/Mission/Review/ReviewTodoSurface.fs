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

/// Review-owned process-review membrane. Magic Todo's typed fact codec and
/// TodoProcessReviewProgram remain internal; tests use JSON-shaped fact payloads
/// and receive named outcomes, never Fable unions or maps.
[<RequireQualifiedAccess>]
module ReviewTodoSurface =

    let private text (value: obj) =
        if isNull value then "" else string value

    let private field (value: obj) (name: string) : obj =
        if isNull value then null else emitJsExpr (value, name) "$0[$1]"

    let private strField value name = text (field value name)

    let private intField value name = unbox<int> (field value name)

    let private boolField value name = unbox<bool> (field value name)

    let private cursorOf value =
        { Sequence =
            let nested = field value "Sequence"
            if isNull nested then int64 (unbox<int> value) else int64 (unbox<int> nested) }

    let private blobRefOf value = BlobRef.create (text value)
    let private blobDigestOf value = BlobDigest.create (text value)
    let private lifeOf value = ManagerLifeId.create (text value)
    let private writeOf value = TodoWriteId.create (text value)
    let private reviewOf value = TodoReviewId.create (text value)
    let private dedicatedOf value = DedicatedReviewerId.create (text value)
    let private sessionOf value = SessionId.create (text value)
    let private callOf value = ToolCallId.create (text value)
    let private runOf value = ProviderRunIdentity.create (text value)

    let private verdictOf value =
        match text value with
        | "REVISE"
        | "Revise" -> ProcessReviewVerdict.Revise
        | _ -> ProcessReviewVerdict.Perfect

    let private physicalEvidenceOf value =
        match text value with
        | "RecoveredCompletedToolPart" -> PhysicalSuccessEvidence.RecoveredCompletedToolPart
        | _ -> PhysicalSuccessEvidence.LiveAfterSuccess

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
        | "DedicatedTodoReviewerEnlisted" ->
            let enlisted: DedicatedTodoReviewerEnlisted =
                { ManagerLifeId = lifeOf (field payload "ManagerLifeId")
                  DedicatedReviewerId = dedicatedOf (field payload "DedicatedReviewerId")
                  ReviewerSessionId = sessionOf (field payload "ReviewerSessionId") }

            MagicTodoFact.DedicatedTodoReviewerEnlisted enlisted
        | "TodoProcessReviewAssigned" ->
            let assigned: TodoProcessReviewAssigned =
                { ManagerLifeId = lifeOf (field payload "ManagerLifeId")
                  TodoWriteId = writeOf (field payload "TodoWriteId")
                  TodoReviewId = reviewOf (field payload "TodoReviewId")
                  DedicatedReviewerId = dedicatedOf (field payload "DedicatedReviewerId")
                  ReviewerSessionId = sessionOf (field payload "ReviewerSessionId")
                  ReviewWorkStartCursor = cursorOf (field payload "ReviewWorkStartCursor")
                  ManagerReviewFrontier = cursorOf (field payload "ManagerReviewFrontier") }

            MagicTodoFact.TodoProcessReviewAssigned assigned
        | "TodoReviewConcluded" ->
            let concluded: TodoReviewConcluded =
                { ManagerLifeId = lifeOf (field payload "ManagerLifeId")
                  TodoWriteId = writeOf (field payload "TodoWriteId")
                  TodoReviewId = reviewOf (field payload "TodoReviewId")
                  DedicatedReviewerId = dedicatedOf (field payload "DedicatedReviewerId")
                  ReviewerSessionId = sessionOf (field payload "ReviewerSessionId")
                  Verdict = verdictOf (field payload "Verdict")
                  WorkRecordRef = blobRefOf (field payload "WorkRecordRef")
                  WorkRecordDigest = blobDigestOf (field payload "WorkRecordDigest")
                  SettledTodoRef = blobRefOf (field payload "SettledTodoRef")
                  SettledTodoDigest = blobDigestOf (field payload "SettledTodoDigest")
                  ReviewerRecordFrontier = cursorOf (field payload "ReviewerRecordFrontier")
                  ProviderRunId = runOf (field payload "ProviderRunId")
                  ToolCallId = callOf (field payload "ToolCallId") }

            MagicTodoFact.TodoReviewConcluded concluded
        | other -> failwith $"ReviewTodoSurface: unknown Magic Todo fact '{other}'"

    let factJson (caseName: string) (payload: obj) = MagicTodoFactCodec.encode (factOf caseName payload)

    let ids (sha256: obj) (lifeId: string) (callId: string) : obj =
        let hash (value: string) = emitJsExpr (sha256, value) "$0($1)"
        let life = ManagerLifeId.create lifeId
        let call = ToolCallId.create callId
        let write = MagicTodo.todoWriteId hash life call
        let review = MagicTodo.todoReviewId hash life write
        let dedicated = MagicTodo.dedicatedReviewerId hash life

        box
            {| todoWriteId = TodoWriteId.value write
               todoReviewId = TodoReviewId.value review
               dedicatedReviewerId = DedicatedReviewerId.value dedicated |}

    let newProjection () = MagicTodoProjectionSurface.create ()

    let fold (projection: MagicTodoProjectionHandle) (eventId: string) (caseName: string) (payload: obj) : obj =
        MagicTodoProjectionSurface.fold projection eventId (factJson caseName payload)

    let view (projection: MagicTodoProjectionHandle) (lifeId: string) : obj =
        MagicTodoProjectionSurface.view projection lifeId

    let appendFact
        (handle: JournalHandle)
        (sessionId: string)
        (providerRun: obj)
        (caseName: string)
        (payload: obj)
        : Task<obj> =
        ObligationJournalSurface.appendMagicTodo handle sessionId providerRun (factJson caseName payload)

    let tryConclude (handle: JournalHandle) (lifeId: string) (writeId: string) : Task<obj> =
        task {
            let! outcome = TodoProcessReviewProgram.tryConclude handle.Journal (ManagerLifeId.create lifeId) (TodoWriteId.create writeId)

            return
                match outcome with
                | TodoProcessReviewProgram.ConcludeOutcome.Concluded -> box {| status = "Concluded" |}
                | TodoProcessReviewProgram.ConcludeOutcome.Pending reason -> box {| status = "Pending"; reason = reason |}
                | TodoProcessReviewProgram.ConcludeOutcome.Failed reason -> box {| status = "Failed"; reason = reason |}
        }

    let producerPresence (handle: JournalHandle) (lifeId: string) (writeId: string) : obj =
        match TodoProcessReviewProgram.producerPresence handle.Journal (ManagerLifeId.create lifeId) (TodoWriteId.create writeId) with
        | TodoProcessReviewProgram.ProducerPresence.Present -> box {| status = "Present" |}
        | TodoProcessReviewProgram.ProducerPresence.Absent reason -> box {| status = "Absent"; reason = reason |}

    let requestKindNames () = [| "TodoProcess"; "FinalityTerminal" |]

    let needsEnsureReview (accepted: bool) (concluded: bool) =
        MagicTodoProcessReview.needsEnsureReview accepted concluded

    let renderAssignmentUserMessage (preamble: string) (request: obj) : string =
        let obligations value =
            if isNull value then
                []
            else
                let values: obj array = unbox<obj array> value
                values |> Array.toList |> List.map (fun item -> { Name = strField item "name"; Work = strField item "work" })

        MagicTodoProcessReview.renderAssignmentUserMessage
            preamble
            { TodoReviewId = TodoReviewId.create (strField request "TodoReviewId")
              TodoWriteId = TodoWriteId.create (strField request "TodoWriteId")
              ManagerLifeId = ManagerLifeId.create (strField request "ManagerLifeId")
              OpeningRaw = strField request "OpeningRaw"
              ManagerCheckpointLwr = strField request "ManagerCheckpointLwr"
              EffectivePlanComplete = boolField request "EffectivePlanComplete"
              OldTodo = obligations (field request "OldTodo")
              ProposedTodo = obligations (field request "ProposedTodo") }

    let awaitConsumableReview (handle: JournalHandle) (lifeId: string) (writeId: string) : Task<obj> =
        task {
            let! result =
                TodoProcessReviewProgram.awaitConsumableReview
                    handle.Journal
                    (ManagerLifeId.create lifeId)
                    (TodoWriteId.create writeId)

            return
                match result with
                | Ok() -> box {| ok = true |}
                | Error error -> box {| ok = false; error = error |}
        }
