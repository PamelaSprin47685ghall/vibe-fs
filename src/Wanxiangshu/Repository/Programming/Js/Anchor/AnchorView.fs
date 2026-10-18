namespace Wanxiangshu.Repository.Programming.Js.Anchor

/// ANCHOR view application. Pure: identity numbering, token counting and the
/// accumulated replacement map are parameters so the same selection can be
/// driven from the transform pipeline and from semantic tests.
[<RequireQualifiedAccess>]
module AnchorView =

    type AnchorMessage =
        { Id: string option
          Text: string
          Raw: obj }

    /// One selected message: kept as it stands, or standing in for a replaced
    /// anchor as the summary the model declared. Building the replacement
    /// message is the caller's job — this module never touches host shapes.
    /// Rows folded into an open replacement simply do not appear.
    type AnchorViewEntry =
        | Kept of AnchorMessage
        | Summarized of message: AnchorMessage * summary: string

    type AnchorViewReport =
        { Total: int
          Used: int
          Cached: int
          Kept: int
          Summarized: int
          Folded: int
          Replaced: int list }

    /// Apply one declaration to a message list.
    ///
    /// `replaced` is the accumulated anchor→summary map. A replaced anchor stops
    /// being an anchor: its row stands in as the summary, and that summary's span
    /// runs to the next anchor that still exists (left-closed, right-open at
    /// anchor boundaries). Anything already replaced inside that span — an
    /// earlier turn's summary included — is folded into it and disappears.
    /// Unnamed rows are never dropped: the view never discards what it cannot
    /// name. `Cached` counts the protected prefix: the kept anchors below
    /// `PrefixUntil`.
    let apply
        (numberOf: string -> int)
        (countTokens: string -> int)
        (replaced: Map<int, string>)
        (declaration: AnchorViewDeclaration)
        (messages: AnchorMessage list)
        : AnchorViewEntry list * AnchorViewReport =
        let numbered =
            messages |> List.map (fun message -> message, message.Id |> Option.map numberOf)

        let rec select rows spanOpen acc =
            match rows with
            | [] -> List.rev acc
            | (message, anchorOpt) :: rest -> advance rest message anchorOpt spanOpen acc

        and advance rest message anchorOpt spanOpen acc =
            match anchorOpt |> Option.filter (fun anchor -> Map.containsKey anchor replaced) with
            | Some anchor when not spanOpen ->
                // The replacement is not an anchor: its span swallows every
                // following row up to the next anchor that still exists.
                select rest true (Summarized(message, replaced.[anchor]) :: acc)
            | Some _ -> select rest true acc
            | None -> select rest false (Kept message :: acc)

        let entries = select numbered false []

        let keptMessages =
            entries
            |> List.choose (function
                | Kept message -> Some message
                | Summarized _ -> None)

        let summaries =
            entries
            |> List.choose (function
                | Summarized(_, summary) -> Some summary
                | Kept _ -> None)

        let kept = entries.Length - summaries.Length
        let summarized = summaries.Length
        let folded = messages.Length - entries.Length

        let total = messages |> List.sumBy (fun message -> countTokens message.Text)

        let cached =
            keptMessages
            |> List.sumBy (fun message ->
                match message.Id |> Option.map numberOf with
                | Some anchor when anchor < declaration.PrefixUntil -> countTokens message.Text
                | _ -> 0)

        let used =
            (keptMessages |> List.sumBy (fun message -> countTokens message.Text))
            + (summaries |> List.sumBy countTokens)

        entries,
        { Total = total
          Used = used
          Cached = cached
          Kept = kept
          Summarized = summarized
          Folded = folded
          Replaced = replaced |> Map.toList |> List.map fst }
