namespace Wanxiangshu.Participant.Provider.Projection

/// Canonical wire messages and the Host metadata aligned with them.
type RenderedMessages =
    { Messages: ProviderProjection.WireMessage list
      HostMessageIds: string option list
      HostIsPhysical: bool list }

[<RequireQualifiedAccess>]
module ProjectionRenderer =

    let private intentRows (intent: ProjectionIntent) : ProjectionMessageRow array =
        match intent with
        | ProjectionIntent.ReplaceMessageBase value -> List.toArray value.Rows
        | ProjectionIntent.InsertMessageRows value -> List.toArray value.Rows

    /// 规范顺序与锚点拼接由 wanxiangli/projection `layout` 决定；宿主只按来源取回行与元数据。
    let private renderOrdered
        (baseMessages: ProviderProjection.WireMessage list)
        (intents: ProjectionIntent list)
        : RenderedMessages =
        let baseRows = List.toArray baseMessages
        let sources = intents |> List.toArray |> Array.map intentRows

        let rows =
            ProjectionPlanner.layout intents baseRows.Length
            |> List.map (fun (intent, row) ->
                match intent with
                | None -> baseRows[row], None, false
                | Some index ->
                    let value = sources[index][row]
                    value.Message, value.HostMessageId, value.HostIsPhysical)

        { Messages = rows |> List.map (fun (message, _, _) -> message)
          HostMessageIds = rows |> List.map (fun (_, id, _) -> id)
          HostIsPhysical = rows |> List.map (fun (_, _, physical) -> physical) }

    /// Plan and materialize generic rows into canonical wire and Host side-channel lists.
    let renderMessagesWithHostIds
        (_snapshot: ProjectionSnapshot)
        (baseMessages: ProviderProjection.WireMessage list)
        (intents: ProjectionIntent list)
        : RenderedMessages =
        match ProjectionPlanner.plan intents with
        | Error _ -> invalidOp "ProjectionRenderer.renderMessagesWithHostIds requires a conflict-free intent set"
        | Ok _ -> renderOrdered baseMessages intents

    /// Render only the canonical wire message list.
    let renderMessagesWithIntents
        (snapshot: ProjectionSnapshot)
        (baseMessages: ProviderProjection.WireMessage list)
        (intents: ProjectionIntent list)
        : ProviderProjection.WireMessage list =
        (renderMessagesWithHostIds snapshot baseMessages intents).Messages

    /// Digest the canonical semantic projection up to the exclusive message cutoff.
    let cutoffDigest (sha256: string -> string) (snapshot: ProjectionSnapshot) (cutoff: int) : string =
        let truncated =
            { snapshot.CurrentProjection with
                Messages = snapshot.CurrentProjection.Messages |> List.truncate cutoff }

        sha256 (ProviderProjection.renderSemantic truncated)
