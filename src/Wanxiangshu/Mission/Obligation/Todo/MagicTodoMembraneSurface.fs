namespace Wanxiangshu.Mission.Obligation.Todo

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Mission.Relay
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts

/// JS-native effect-shell owner for the Magic Todo membrane.
/// Journal, snapshot, and review ports stay opaque resources; only durable
/// receipts and provider-visible outcomes cross the boundary.
type MagicTodoPreparedHandle private (bridge: MagicTodoMembrane.PreparedBridge) =
    member internal _.Bridge = bridge
    static member Create(bridge: MagicTodoMembrane.PreparedBridge) = MagicTodoPreparedHandle(bridge)

[<RequireQualifiedAccess>]
module MagicTodoMembraneSurface =

    type private ControlledSnapshot(rawMessages: obj array) =
        interface ISessionSnapshotPort with
            member _.GetMessages(_sessionId) =
                Task.FromResult(Ok(SessionSnapshotPort.projectMessages rawMessages))

    let private text (value: obj) =
        if isNull value then "" else string value

    let private stateOf (value: obj) : SnapshotToolPartState =
        match if isNull value then 0 else unbox<int> value with
        | 1 -> SnapshotToolPartState.Completed ""
        | 2 -> SnapshotToolPartState.Failed ""
        | _ -> SnapshotToolPartState.Pending

    let private cursor (sequence: int) : XTraceCursor = XTraceCursor.create (int64 sequence)

    let private localizedOf
        (callId: string)
        (inputCanonical: string)
        (state: obj)
        : MagicTodoLocality.LocalizedToolCall =
        let frontier = cursor 7

        { ProviderRun = ProviderRunIdentity.create "msg-provider-run"
          HostToolPartId = HostToolPartId.create "prt-todowrite"
          ToolCallId = ToolCallId.create callId
          ToolName = "todowrite"
          InputCanonical = inputCanonical
          State = stateOf state
          TodowriteCallIdsInMessage = [ ToolCallId.create callId ]
          ToolPartOrdinal = 1
          ReviewFrontier = frontier
          Range = XTraceRange.create frontier (cursor 8) }

    let private obligationsOf (values: obj array) : ObligationList =
        if isNull values then
            []
        else
            values
            |> Array.toList
            |> List.map (fun value ->
                let horizonText = text (value?horizon)

                let horizon =
                    if String.IsNullOrWhiteSpace horizonText then
                        ObligationHorizon.Near
                    else
                        horizonText
                        |> ObligationHorizon.tryParse
                        |> Option.defaultWith (fun () -> invalidArg "horizon" "expected near, mid, or far")

                { Name = text (value?name)
                  Horizon = horizon
                  Work = text (value?work) })

    let private blobView reference digest =
        box
            {| reference = BlobRef.value reference
               digest = BlobDigest.value digest |}

    let private preparedView (bridge: MagicTodoMembrane.PreparedBridge) : obj =
        box
            {| incumbencyId = IncumbencyId.value bridge.Prepared.IncumbencyId
               todoWriteId = TodoWriteId.value bridge.Prepared.TodoWriteId
               toolCallId = ToolCallId.value bridge.Prepared.ToolCallId
               planCompleteDeclared = bridge.Prepared.PlanCompleteDeclared
               providerInputDigest = bridge.Prepared.ProviderInputDigest
               baseTodo = blobView bridge.Prepared.BaseTodoRef bridge.Prepared.BaseTodoDigest
               proposedTodo = blobView bridge.Prepared.ProposedTodoRef bridge.Prepared.ProposedTodoDigest
               proposedTodoRef = BlobRef.value bridge.Prepared.ProposedTodoRef
               proposedTodoDigest = BlobDigest.value bridge.Prepared.ProposedTodoDigest
               baseTodoRef = BlobRef.value bridge.Prepared.BaseTodoRef
               baseTodoDigest = BlobDigest.value bridge.Prepared.BaseTodoDigest |}

    let private rejectionView rejection : obj =
        let code =
            match rejection with
            | MagicTodoMembrane.PrepareRejection.NoActiveIncumbency -> "NoActiveIncumbency"
            | MagicTodoMembrane.PrepareRejection.UnexpectedToolName _ -> "UnexpectedToolName"
            | MagicTodoMembrane.PrepareRejection.SnapshotInputMismatch -> "SnapshotInputMismatch"
            | MagicTodoMembrane.PrepareRejection.Admission(MagicTodoReject.MultipleTodowriteInMessage _) ->
                "MultipleTodowriteInMessage"
            | MagicTodoMembrane.PrepareRejection.Admission(MagicTodoReject.EmptyObligationName _) ->
                "EmptyObligationName"
            | MagicTodoMembrane.PrepareRejection.Admission(MagicTodoReject.DuplicateObligationName _) ->
                "DuplicateObligationName"
            | MagicTodoMembrane.PrepareRejection.Admission(MagicTodoReject.IdentityCorruption _) -> "IdentityCorruption"
            | MagicTodoMembrane.PrepareRejection.Admission(MagicTodoReject.FirstSuicideWithoutCheckpoint) ->
                "FirstSuicideWithoutCheckpoint"
            | MagicTodoMembrane.PrepareRejection.BlobRead _ -> "BlobRead"
            | MagicTodoMembrane.PrepareRejection.BlobWrite _ -> "BlobWrite"
            | MagicTodoMembrane.PrepareRejection.BlobDigestMismatch _ -> "BlobDigestMismatch"
            | MagicTodoMembrane.PrepareRejection.BlobDecode _ -> "BlobDecode"
            | MagicTodoMembrane.PrepareRejection.JournalAppend _ -> "JournalAppend"
            | MagicTodoMembrane.PrepareRejection.ProjectionInconsistent _ -> "ProjectionInconsistent"

        box
            {| code = code
               detail = sprintf "%A" rejection |}

    let private acceptRejectionView rejection : obj =
        match rejection with
        | MagicTodoMembrane.AcceptRejection.InputDigestMismatch -> box {| code = "InputDigestMismatch" |}
        | MagicTodoMembrane.AcceptRejection.OutputDigestMismatch -> box {| code = "OutputDigestMismatch" |}
        | MagicTodoMembrane.AcceptRejection.JournalAppend reason ->
            box
                {| code = "JournalAppend"
                   detail = reason |}

    let private physicalResult value : Result<PhysicalSuccessEvidence, string> =
        match text value with
        | "RecoveredCompletedToolPart" -> Ok PhysicalSuccessEvidence.RecoveredCompletedToolPart
        | "LiveAfterSuccess" -> Ok PhysicalSuccessEvidence.LiveAfterSuccess
        | unknown -> Error(sprintf "unknown physical success evidence: %s" unknown)

    let private physicalOf value =
        match physicalResult value with
        | Ok evidence -> evidence
        | Error error -> invalidArg "physicalEvidence" error

    let prepare
        (handle: JournalHandle)
        (sessionId: string)
        (callId: string)
        (inputCanonical: string)
        (providerInputDigest: string)
        (planComplete: bool)
        (obligations: obj array)
        (state: obj)
        : Task<obj> =
        task {
            let localized = localizedOf callId inputCanonical state

            let! result =
                MagicTodoMembrane.prepare
                    handle.Journal
                    (SessionId.create sessionId)
                    localized
                    providerInputDigest
                    planComplete
                    (obligationsOf obligations)

            return
                match result with
                | Error rejection ->
                    box
                        {| ok = false
                           error = rejectionView rejection |}
                | Ok bridge ->
                    box
                        {| ok = true
                           value =
                            box
                                {| bridge = MagicTodoPreparedHandle.Create bridge
                                   prepared = preparedView bridge
                                   submitted =
                                    bridge.SubmittedObligations
                                    |> List.map (fun value ->
                                        box
                                            {| name = value.Name
                                               work = value.Work |})
                                    |> List.toArray |} |}
        }

    let accept
        (handle: JournalHandle)
        (prepared: MagicTodoPreparedHandle)
        (physicalEvidence: string)
        (observedInputDigest: string)
        (observedOutputDigest: string)
        : Task<obj> =
        match physicalResult (box physicalEvidence) with
        | Error error ->
            Task.FromResult(
                box
                    {| ok = false
                       error =
                        box
                            {| code = "InvalidPhysicalEvidence"
                               detail = error |} |}
            )
        | Ok physicalEvidence ->
            task {
                let! result =
                    MagicTodoMembrane.accept
                        handle.Journal
                        prepared.Bridge
                        physicalEvidence
                        observedInputDigest
                        observedOutputDigest

                return
                    match result with
                    | Error rejection ->
                        box
                            {| ok = false
                               error = acceptRejectionView rejection |}
                    | Ok outcome ->
                        box
                            {| ok = true
                               value = box {| enrichedResult = outcome.EnrichedResult |} |}
            }

    let appendFact (handle: JournalHandle) (sessionId: string) (factJson: string) : Task<obj> =
        ObligationJournalSurface.appendMagicTodo handle sessionId null factJson

    let snapshot (handle: JournalHandle) (incumbencyId: string) : obj =
        ObligationJournalSurface.snapshotMagicTodo handle incumbencyId

    /// Real Host Before -> controlled builtin executor -> After workflow. Only
    /// successful return from the supplied physical executor reaches After;
    /// no PhysicalSuccessEvidence value crosses this boundary.
    let executeHostSuccess
        (handle: JournalHandle)
        (rawMessages: obj array)
        (sessionId: string)
        (incumbencyId: string)
        (callId: string)
        (args: obj)
        (executor: obj)
        : Task<obj> =
        task {
            let snapshots = ControlledSnapshot(rawMessages) :> ISessionSnapshotPort
            let hooks = MagicTodoHostHooks.create (Some handle.Journal) (Some snapshots)

            let input =
                createObj [ "tool" ==> "todowrite"; "sessionID" ==> sessionId; "callID" ==> callId ]

            let beforeOutput = createObj [ "args" ==> args ]

            do! hooks.Before input beforeOutput
            let hostOutput: obj = emitJsExpr (executor, beforeOutput?args) "$0($1)"
            do! hooks.After input hostOutput

            return
                box
                    {| output = hostOutput
                       incumbency = ObligationJournalSurface.snapshotMagicTodo handle incumbencyId |}
        }
