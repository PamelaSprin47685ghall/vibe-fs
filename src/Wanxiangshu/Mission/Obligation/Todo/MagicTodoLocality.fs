namespace Wanxiangshu.Mission.Obligation.Todo

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation
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
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Trace
open Wanxiangshu.OpenCode
open Wanxiangshu.Foundation.Identity

/// Durable localization for one V1 Host tool callback.
///
/// The callback exposes only `(sessionID, callID)`. This resolver proves its
/// assistant provider run and physical ToolPart from the persisted snapshot,
/// then joins that exact triple to the captured XTrace part. Any missing or
/// ambiguous link is fail-closed; neither message-array position nor a guessed
/// provider run is admissible evidence.
module MagicTodoLocality =

    type XTraceRange =
        { Start: XTraceCursor
          EndExclusive: XTraceCursor }

    type LocalizedToolCall =
        { ProviderRun: ProviderRunIdentity
          HostToolPartId: HostToolPartId
          ToolCallId: ToolCallId
          ToolName: string
          InputCanonical: string
          State: SnapshotToolPartState
          TodowriteCallIdsInMessage: ToolCallId list
          ToolPartOrdinal: int
          ReviewFrontier: XTraceCursor
          Range: XTraceRange }

    [<RequireQualifiedAccess>]
    type LocalityRejection =
        | Snapshot of SessionSnapshotPort.ToolCallLocationError
        | XTraceUnavailable
        | XTraceMissing of providerRun: ProviderRunIdentity * toolCallId: ToolCallId * hostToolPartId: HostToolPartId
        | XTraceAmbiguous of providerRun: ProviderRunIdentity * toolCallId: ToolCallId * hostToolPartId: HostToolPartId

    [<RequireQualifiedAccess>]
    type InputMaterializationRejection =
        | SnapshotUnavailable of reason: string
        | Snapshot of SessionSnapshotPort.ToolCallLocationError
        | CarrierChanged
        | InputMismatch

    let private stepCapturablePart callId (part: MessagePart) index count loop =
        match part with
        | MessagePart.Activity _ -> loop (index + 1) count
        | MessagePart.ToolCall(id, _, _) when id = callId -> count
        | MessagePart.ToolResult(id, _) when id = callId -> count
        | _ -> loop (index + 1) (count + 1)

    /// Capturable XTrace parts in this Host message before `toolCallId`.
    /// Activity is transport-only and does not occupy an XTrace cursor.
    let private capturablePartsBeforeTool (parts: MessagePart array) (toolCallId: ToolCallId) : int =
        let callId = ToolCallId.value toolCallId

        let rec loop index count =
            if index >= parts.Length then
                0
            else
                stepCapturablePart callId parts.[index] index count loop

        loop 0 0

    /// Exclusive cursor immediately before this pending tool-call.
    /// Next-assigned alone is the last assistant text when that text is still
    /// uncaptured — Before(Tk) must skip those preceding capturable parts.
    let private pendingReviewFrontier
        (trace: XTraceProjectionState)
        (message: SessionMessage)
        (providerRun: ProviderRunIdentity)
        (toolCallId: ToolCallId)
        =
        let expectedPrefixCount = capturablePartsBeforeTool message.Parts toolCallId

        let capturedPrefixCount =
            XTraceProjection.parts trace
            |> List.filter (fun part -> part.ProviderRun = Some providerRun)
            |> List.length
            |> min expectedPrefixCount

        let missingPrefixCount = expectedPrefixCount - capturedPrefixCount

        { Sequence = XTraceProjection.headSequence trace + 1L + int64 missingPrefixCount }

    let private isUnmaterializedPendingStub (part: SessionToolPart) =
        part.State = SnapshotToolPartState.Pending
        && (String.IsNullOrWhiteSpace part.InputCanonical
            || part.InputCanonical = "{}"
            || part.InputCanonical = "null")

    let private semanticTodowriteCallIds (currentCallId: ToolCallId) (parts: SessionToolPart array) =
        parts
        |> Array.filter (fun part ->
            part.ToolName = "todowrite"
            && (part.ToolCallId = currentCallId || not (isUnmaterializedPendingStub part)))
        |> Array.map (fun part -> part.ToolCallId)
        |> Array.distinct
        |> Array.toList

    let private assistantMessageForRun (messages: SessionMessage list) (providerRun: ProviderRunIdentity) =
        let run = ProviderRunIdentity.value providerRun

        messages
        |> List.filter (fun message -> message.Role = "assistant" && message.Id = run)

    let private resolveCapturedToolCall
        trace
        messages
        (located: SessionSnapshotPort.ToolCallLocation)
        (part: XTracePartRef)
        =
        let toolPartOrdinal =
            XTraceProjection.parts trace
            |> List.filter (fun candidate ->
                candidate.ProviderRun = Some located.ProviderRun
                && candidate.Kind = "tool_call"
                && candidate.Cursor.Sequence <= part.Cursor.Sequence)
            |> List.distinctBy (fun candidate ->
                candidate.HostToolPartId
                |> Option.map HostToolPartId.value
                |> Option.defaultValue candidate.Provenance)
            |> List.length

        let todowriteCallIdsResult =
            assistantMessageForRun messages located.ProviderRun
            |> List.map (fun message -> semanticTodowriteCallIds located.ToolCallId message.ToolParts)

        match todowriteCallIdsResult with
        | [ todowriteCallIds ] ->
            Ok
                { ProviderRun = located.ProviderRun
                  HostToolPartId = located.HostToolPartId
                  ToolCallId = located.ToolCallId
                  ToolName = located.ToolName
                  InputCanonical = located.InputCanonical
                  State = located.State
                  TodowriteCallIdsInMessage = todowriteCallIds
                  ToolPartOrdinal = toolPartOrdinal
                  ReviewFrontier = part.Cursor
                  Range =
                    { Start = part.Cursor
                      EndExclusive = { Sequence = part.Cursor.Sequence + 1L } } }
        | _ -> Error(LocalityRejection.XTraceMissing(located.ProviderRun, located.ToolCallId, located.HostToolPartId))

    let private resolvePendingInMessage
        trace
        (located: SessionSnapshotPort.ToolCallLocation)
        (message: SessionMessage)
        =
        let partMatches =
            message.ToolParts
            |> Array.mapi (fun index part -> index, part)
            |> Array.filter (fun (_, part) ->
                part.HostToolPartId = located.HostToolPartId
                && part.ToolCallId = located.ToolCallId)

        match Array.toList partMatches with
        | [ (index, _) ] ->
            let frontier =
                pendingReviewFrontier trace message located.ProviderRun located.ToolCallId

            let todowriteCallIds = semanticTodowriteCallIds located.ToolCallId message.ToolParts

            Ok
                { ProviderRun = located.ProviderRun
                  HostToolPartId = located.HostToolPartId
                  ToolCallId = located.ToolCallId
                  ToolName = located.ToolName
                  InputCanonical = located.InputCanonical
                  State = located.State
                  TodowriteCallIdsInMessage = todowriteCallIds
                  ToolPartOrdinal = index + 1
                  ReviewFrontier = frontier
                  Range =
                    { Start = frontier
                      EndExclusive = { Sequence = frontier.Sequence + 1L } } }
        | _ -> Error(LocalityRejection.XTraceMissing(located.ProviderRun, located.ToolCallId, located.HostToolPartId))

    let private resolvePendingAssistantMessage trace messages (located: SessionSnapshotPort.ToolCallLocation) =
        let providerRunText = ProviderRunIdentity.value located.ProviderRun

        let messageMatches =
            messages
            |> List.filter (fun message -> message.Role = "assistant" && message.Id = providerRunText)

        match messageMatches with
        | [ message ] -> resolvePendingInMessage trace located message
        | _ -> Error(LocalityRejection.XTraceMissing(located.ProviderRun, located.ToolCallId, located.HostToolPartId))

    let private resolvePendingToolCall
        trace
        (messages: SessionMessage list)
        (located: SessionSnapshotPort.ToolCallLocation)
        =
        if located.State <> SnapshotToolPartState.Pending then
            Error(LocalityRejection.XTraceMissing(located.ProviderRun, located.ToolCallId, located.HostToolPartId))
        else
            resolvePendingAssistantMessage trace messages located

    let private sameCapturedToolObservation (expected: XTracePartRef) (candidate: XTracePartRef) =
        candidate.Role = expected.Role
        && candidate.Kind = expected.Kind
        && candidate.ToolName = expected.ToolName
        && candidate.TextDigest = expected.TextDigest

    let private collapseIdenticalPhysicalReplays (matches: XTracePartRef list) =
        match matches with
        | first :: rest when rest |> List.forall (sameCapturedToolObservation first) -> Some first
        | _ -> None

    let private resolveDuplicateCapturedTool
        trace
        messages
        (located: SessionSnapshotPort.ToolCallLocation)
        (duplicates: XTracePartRef list)
        =
        match collapseIdenticalPhysicalReplays duplicates with
        | Some first -> resolveCapturedToolCall trace messages located first
        | None ->
            Error(LocalityRejection.XTraceAmbiguous(located.ProviderRun, located.ToolCallId, located.HostToolPartId))

    let private resolveLocated trace messages (located: SessionSnapshotPort.ToolCallLocation) =
        let matches =
            XTraceProjection.parts trace
            |> List.filter (fun part ->
                part.ProviderRun = Some located.ProviderRun
                && part.ToolCallId = Some located.ToolCallId
                && part.HostToolPartId = Some located.HostToolPartId)

        match matches with
        | [ part ] -> resolveCapturedToolCall trace messages located part
        | [] -> resolvePendingToolCall trace messages located
        | duplicates -> resolveDuplicateCapturedTool trace messages located duplicates

    let private resolveWithTrace messages (located: SessionSnapshotPort.ToolCallLocation) xTrace =
        match xTrace with
        | None -> Error LocalityRejection.XTraceUnavailable
        | Some trace -> resolveLocated trace messages located

    let resolve
        (sessionId: SessionId)
        (messages: SessionMessage list)
        (projection: ProjectionSet)
        (toolCallId: ToolCallId)
        : Result<LocalizedToolCall, LocalityRejection> =
        match SessionSnapshotPort.locateToolCall toolCallId messages with
        | Error error -> Error(LocalityRejection.Snapshot error)
        | Ok located ->
            let xTrace =
                AgentProjection.tryFind sessionId projection.AgentProjections
                |> Option.bind (fun session -> session.XTrace)

            resolveWithTrace messages located xTrace

    let materializeInput
        (localized: LocalizedToolCall)
        (expectedInputCanonical: string)
        : Result<LocalizedToolCall, InputMaterializationRejection> =
        if localized.InputCanonical = expectedInputCanonical then
            Ok localized
        elif
            localized.State = SnapshotToolPartState.Pending
            && (localized.InputCanonical = "{}"
                || String.IsNullOrWhiteSpace localized.InputCanonical)
        then
            Ok
                { localized with
                    InputCanonical = expectedInputCanonical }
        else
            Error InputMaterializationRejection.InputMismatch
