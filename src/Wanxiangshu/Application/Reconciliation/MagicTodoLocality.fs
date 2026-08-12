namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Kernel
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

    [<RequireQualifiedAccess>]
    type InputMaterializationRejection =
        | SnapshotUnavailable of reason: string
        | Snapshot of SessionSnapshotPort.ToolCallLocationError
        | CarrierChanged
        | InputMismatch

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
                    // The Host invokes tool.execute.before after persisting the
                    // pending ToolPart but before the event observer appends that
                    // same part to XTrace. In that narrow window the full SDK
                    // snapshot is already authoritative for provider-run / part /
                    // call identity. The XTrace head is therefore exactly the
                    // exclusive frontier immediately before this call.
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
                                let frontier = { Sequence = XTraceProjection.headSequence trace + 1L }

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

    let rec awaitMaterializedInput
        (snapshot: ISessionSnapshotPort)
        (sessionId: SessionId)
        (localized: LocalizedToolCall)
        (expectedInputCanonical: string)
        : Task<Result<LocalizedToolCall, InputMaterializationRejection>> =
        task {
            if localized.InputCanonical = expectedInputCanonical then
                return Ok localized
            elif
                localized.State <> SnapshotToolPartState.Pending
                || localized.InputCanonical <> "{}"
            then
                return Error InputMaterializationRejection.InputMismatch
            else
                let! messagesResult = snapshot.GetMessages sessionId

                match messagesResult with
                | Error reason -> return Error(InputMaterializationRejection.SnapshotUnavailable reason)
                | Ok messages ->
                    match SessionSnapshotPort.locateToolCall localized.ToolCallId messages with
                    | Error reason -> return Error(InputMaterializationRejection.Snapshot reason)
                    | Ok located ->
                        if
                            located.ProviderRun <> localized.ProviderRun
                            || located.HostToolPartId <> localized.HostToolPartId
                            || located.ToolName <> localized.ToolName
                        then
                            return Error InputMaterializationRejection.CarrierChanged
                        else
                            let refreshed =
                                { localized with
                                    InputCanonical = located.InputCanonical
                                    State = located.State }

                            if refreshed.InputCanonical = expectedInputCanonical then
                                return Ok refreshed
                            elif
                                refreshed.State = SnapshotToolPartState.Pending
                                && refreshed.InputCanonical = "{}"
                            then
                                do! Wanxiangshu.Process.PtyTiming.timerTask 10
                                return! awaitMaterializedInput snapshot sessionId refreshed expectedInputCanonical
                            else
                                return Error InputMaterializationRejection.InputMismatch
        }
