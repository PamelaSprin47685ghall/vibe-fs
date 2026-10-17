namespace Wanxiangshu.Repository.Programming.Js.Anchor

/// ANCHOR view application. Pure: identity numbering and token counting are
/// parameters so the same selection can be driven from the transform pipeline
/// and from semantic tests.
[<RequireQualifiedAccess>]
module AnchorView =

    type AnchorMessage =
        { Id: string option
          Text: string
          Raw: obj }

    type AnchorViewReport =
        { Total: int
          Used: int
          Cached: int
          Kept: int
          Dropped: int
          ExtraProse: string list }

    let apply
        (numberOf: string -> int)
        (countTokens: string -> int)
        (declaration: AnchorViewDeclaration)
        (messages: AnchorMessage list)
        : AnchorMessage list * AnchorViewReport =
        let numbered =
            messages |> List.map (fun message -> message, message.Id |> Option.map numberOf)

        let extraAnchors =
            declaration.Extra
            |> List.choose (function
                | AnchorExtra.Anchor anchor -> Some anchor
                | AnchorExtra.Prose _ -> None)
            |> Set.ofList

        let extraProse =
            declaration.Extra
            |> List.choose (function
                | AnchorExtra.Prose text -> Some text
                | AnchorExtra.Anchor _ -> None)

        let inPrefix anchor =
            match declaration.PrefixUntil with
            | None -> true
            | Some boundary -> anchor <= boundary

        let kept, dropped =
            numbered
            |> List.partition (fun (_, anchor) ->
                match anchor with
                | None -> true
                | Some value -> inPrefix value)

        let readded =
            dropped
            |> List.filter (fun (_, anchor) ->
                match anchor with
                | Some value -> Set.contains value extraAnchors
                | None -> false)
            |> List.map fst

        let survivors = (kept |> List.map fst) @ readded

        let total = numbered |> List.sumBy (fun (message, _) -> countTokens message.Text)
        let cached = kept |> List.sumBy (fun (message, _) -> countTokens message.Text)

        let used =
            (survivors |> List.sumBy (fun message -> countTokens message.Text))
            + (extraProse |> List.sumBy countTokens)

        survivors,
        { Total = total
          Used = used
          Cached = cached
          Kept = survivors.Length
          Dropped = messages.Length - survivors.Length
          ExtraProse = extraProse }
