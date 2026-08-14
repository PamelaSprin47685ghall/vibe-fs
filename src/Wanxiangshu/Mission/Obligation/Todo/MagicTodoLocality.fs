namespace Wanxiangshu.Mission.Obligation.Todo

open System
open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Domain
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Trace
open Wanxiangshu.OpenCode
open Wanxiangshu.Kernel.Identity

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

    /// Capturable XTrace parts in this Host message before `toolCallId`.
    /// Activity is transport-only and does not occupy an XTrace cursor.
    let private capturablePartsBeforeTool (parts: MessagePart array) (toolCallId: ToolCallId) : int =
        let callId = ToolCallId.value toolCallId

        let rec loop index count =
            if index >= parts.Length then
                0
            else
                match parts.[index] with
                | MessagePart.Activity _ -> loop (index + 1) count
                | MessagePart.ToolCall(id, _, _) when id = callId -> count
                | MessagePart.ToolResult(id, _) when id = callId -> count
                | _ -> loop (index + 1) (count + 1)

        loop 0 0

    /// Exclusive cursor immediately before this pending tool-call.
    /// Next-assigned alone is the last assistant text when that text is still
    /// uncaptured — Before(Tk) must skip those preceding capturable parts.
    let private pendingReviewFrontier
        (trace: XTraceProjectionState)
        (message: SessionMessage)
        (toolCallId: ToolCallId)
        =
        { Sequence =
            XTraceProjection.headSequence trace
            + 1L
            + int64 (capturablePartsBeforeTool message.Parts toolCallId) }

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

            match xTrace with
            | None -> Error LocalityRejection.XTraceUnavailable
            | Some trace ->
                let matches =
                    XTraceProjection.parts trace
                    |> List.filter (fun part ->
                        part.ProviderRun = Some located.ProviderRun
                        && part.ToolCallId = Some located.ToolCallId
                        && part.HostToolPartId = Some located.HostToolPartId)

                match matches with
                | [] ->
                    // before-hook: ToolPart is persisted, this call is not yet in
                    // XTrace. SDK snapshot is the identity source. Before(Tk) is
                    // the cursor the tool-call will occupy, not next-assigned
                    // (that cursor belongs to preceding uncaptured text).
                    match located.State with
                    | SnapshotToolPartState.Pending ->
                        let providerRunText = ProviderRunIdentity.value located.ProviderRun

                        let messageMatches =
                            messages
                            |> List.filter (fun message -> message.Role = "assistant" && message.Id = providerRunText)

                        match messageMatches with
                        | [ message ] ->
                            let partMatches =
                                message.ToolParts
                                |> Array.mapi (fun index part -> index, part)
                                |> Array.filter (fun (_, part) ->
                                    part.HostToolPartId = located.HostToolPartId
                                    && part.ToolCallId = located.ToolCallId)

                            match Array.toList partMatches with
                            | [ (index, _) ] ->
                                let frontier = pendingReviewFrontier trace message located.ToolCallId

                                let todowriteCallIds =
                                    message.ToolParts
                                    |> Array.filter (fun part -> part.ToolName = "todowrite")
                                    |> Array.map (fun part -> part.ToolCallId)
                                    |> Array.distinct
                                    |> Array.toList

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
                            | _ ->
                                Error(
                                    LocalityRejection.XTraceMissing(
                                        located.ProviderRun,
                                        located.ToolCallId,
                                        located.HostToolPartId
                                    )
                                )
                        | _ ->
                            Error(
                                LocalityRejection.XTraceMissing(
                                    located.ProviderRun,
                                    located.ToolCallId,
                                    located.HostToolPartId
                                )
                            )
                    | SnapshotToolPartState.Completed _
                    | SnapshotToolPartState.Failed _ ->
                        Error(
                            LocalityRejection.XTraceMissing(
                                located.ProviderRun,
                                located.ToolCallId,
                                located.HostToolPartId
                            )
                        )
                | [ part ] ->
                    let toolPartOrdinal =
                        XTraceProjection.parts trace
                        |> List.filter (fun candidate ->
                            candidate.ProviderRun = Some located.ProviderRun
                            && candidate.Kind = "tool_call"
                            && candidate.Cursor.Sequence <= part.Cursor.Sequence)
                        |> List.length

                    let todowriteCallIds =
                        XTraceProjection.parts trace
                        |> List.filter (fun candidate ->
                            candidate.ProviderRun = Some located.ProviderRun
                            && candidate.Kind = "tool_call"
                            && candidate.ToolName = Some "todowrite")
                        |> List.choose (fun candidate -> candidate.ToolCallId)
                        |> List.distinct

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
                | _ ->
                    Error(
                        LocalityRejection.XTraceAmbiguous(
                            located.ProviderRun,
                            located.ToolCallId,
                            located.HostToolPartId
                        )
                    )

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
