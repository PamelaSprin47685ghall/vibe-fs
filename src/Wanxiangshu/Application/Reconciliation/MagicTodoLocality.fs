namespace Wanxiangshu.OpenCode

open Wanxiangshu.Domain
open Wanxiangshu.Journal
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
                    trace.Parts
                    |> List.filter (fun part ->
                        part.ProviderRun = Some located.ProviderRun
                        && part.ToolCallId = Some located.ToolCallId
                        && part.HostToolPartId = Some located.HostToolPartId)

                match matches with
                | [] ->
                    Error(
                        LocalityRejection.XTraceMissing(
                            located.ProviderRun,
                            located.ToolCallId,
                            located.HostToolPartId
                        )
                    )
                | [ part ] ->
                    let toolPartOrdinal =
                        trace.Parts
                        |> List.filter (fun candidate ->
                            candidate.ProviderRun = Some located.ProviderRun
                            && candidate.Kind = "tool_call"
                            && candidate.Cursor.Sequence <= part.Cursor.Sequence)
                        |> List.length

                    let todowriteCallIds =
                        trace.Parts
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
