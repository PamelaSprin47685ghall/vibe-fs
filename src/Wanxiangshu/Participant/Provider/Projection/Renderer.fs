namespace Wanxiangshu.Participant.Provider.Projection

/// Canonical wire messages and the Host metadata aligned with them.
type RenderedMessages =
    { Messages: ProviderProjection.WireMessage list
      HostMessageIds: string option list
      HostIsPhysical: bool list }

[<RequireQualifiedAccess>]
module ProjectionRenderer =

    let private renderedRows (rows: ProjectionMessageRow list) : RenderedMessages =
        { Messages = rows |> List.map (fun row -> row.Message)
          HostMessageIds = rows |> List.map (fun row -> row.HostMessageId)
          HostIsPhysical = rows |> List.map (fun row -> row.HostIsPhysical) }

    let private emptyRendered (baseMessages: ProviderProjection.WireMessage list) : RenderedMessages =
        { Messages = baseMessages
          HostMessageIds = baseMessages |> List.map (fun _ -> None)
          HostIsPhysical = baseMessages |> List.map (fun _ -> false) }

    let private spliceBefore (index: int) (extra: RenderedMessages) (acc: RenderedMessages) : RenderedMessages =
        let beforeMessages, afterMessages = List.splitAt index acc.Messages
        let beforeIds, afterIds = List.splitAt index acc.HostMessageIds
        let beforePhysical, afterPhysical = List.splitAt index acc.HostIsPhysical

        { Messages = beforeMessages @ extra.Messages @ afterMessages
          HostMessageIds = beforeIds @ extra.HostMessageIds @ afterIds
          HostIsPhysical = beforePhysical @ extra.HostIsPhysical @ afterPhysical }

    let private applyInsertions
        (insertions: ProjectionMessageInsertion list)
        (acc: RenderedMessages)
        : RenderedMessages =
        let before, append =
            insertions
            |> List.partition (fun insertion ->
                match insertion.Anchor with
                | ProjectionMessageAnchor.BeforeMessageIndex _ -> true
                | ProjectionMessageAnchor.Append -> false)

        let beforeInApplicationOrder =
            before
            |> List.sortByDescending (fun insertion ->
                match insertion.Anchor with
                | ProjectionMessageAnchor.BeforeMessageIndex index -> index, insertion.Key
                | ProjectionMessageAnchor.Append -> -1, "")

        let appendInApplicationOrder =
            append |> List.sortBy (fun insertion -> insertion.Key)

        (acc, beforeInApplicationOrder @ appendInApplicationOrder)
        ||> List.fold (fun state insertion ->
            let extra = renderedRows insertion.Rows

            match insertion.Anchor with
            | ProjectionMessageAnchor.Append ->
                { Messages = state.Messages @ extra.Messages
                  HostMessageIds = state.HostMessageIds @ extra.HostMessageIds
                  HostIsPhysical = state.HostIsPhysical @ extra.HostIsPhysical }
            | ProjectionMessageAnchor.BeforeMessageIndex index -> spliceBefore index extra state)

    let private renderOrdered
        (baseMessages: ProviderProjection.WireMessage list)
        (ordered: ProjectionIntent list)
        : RenderedMessages =
        let replacement =
            ordered
            |> List.tryPick (function
                | ProjectionIntent.ReplaceMessageBase value -> Some value
                | _ -> None)

        let initial =
            match replacement with
            | Some value -> renderedRows value.Rows
            | None -> emptyRendered baseMessages

        let insertions =
            ordered
            |> List.choose (function
                | ProjectionIntent.InsertMessageRows value -> Some value
                | _ -> None)

        applyInsertions insertions initial

    /// Plan and materialize generic rows into canonical wire and Host side-channel lists.
    let renderMessagesWithHostIds
        (_snapshot: ProjectionSnapshot)
        (baseMessages: ProviderProjection.WireMessage list)
        (intents: ProjectionIntent list)
        : RenderedMessages =
        match ProjectionPlanner.plan intents with
        | Error _ -> invalidOp "ProjectionRenderer.renderMessagesWithHostIds requires a conflict-free intent set"
        | Ok ordered -> renderOrdered baseMessages ordered

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
