namespace Wanxiangshu.Repository.Programming.Js.Anchor

/// Shared view projection. Each operation replaces its captured, latest interval.
[<RequireQualifiedAccess>]
module AnchorView =
    type AnchorMessage =
        { Id: string option
          Text: string
          Raw: obj }

    type AnchorViewEntry =
        | Kept of AnchorMessage
        | Summarized of message: AnchorMessage * summary: string
        | OpeningSummary of summary: string

    type AnchorViewReport =
        { Total: int
          Used: int
          Cached: int
          Kept: int
          Summarized: int
          Folded: int
          Replaced: int list }

    let private textOf =
        function
        | Kept message -> message.Text
        | Summarized(_, summary)
        | OpeningSummary summary -> summary

    let apply
        (numberOf: string -> int)
        (countTokens: string -> int)
        (history: AnchorReplacement list)
        (declaration: AnchorViewDeclaration)
        (messages: AnchorMessage list)
        : AnchorViewEntry list * AnchorViewReport =
        let positions, _ =
            messages
            |> List.mapFold
                (fun position message ->
                    let anchor = message.Id |> Option.map numberOf |> Option.defaultValue position
                    anchor, anchor)
                0

        let inputs = List.toArray messages
        let projection = AnchorCoverage.project positions history declaration.PrefixUntil

        let rows =
            projection.Rows
            |> List.map (function
                | AnchorProjectedRow.Kept(index, cached) -> Kept inputs.[index], cached
                | AnchorProjectedRow.Summarized(index, summary, cached) -> Summarized(inputs.[index], summary), cached
                | AnchorProjectedRow.Opening(summary, cached) -> OpeningSummary summary, cached)

        let entries = rows |> List.map fst

        entries,
        { Total = messages |> List.sumBy (fun message -> countTokens message.Text)
          Used = entries |> List.sumBy (textOf >> countTokens)
          Cached =
            rows
            |> List.sumBy (fun (entry, cached) -> if cached then countTokens (textOf entry) else 0)
          Kept = projection.Kept
          Summarized = projection.Summarized
          Folded = projection.Folded
          Replaced = projection.Replaced }

    let generate numberOf countTokens history declaration messages =
        let anchors =
            messages |> List.choose (fun message -> message.Id |> Option.map numberOf)

        let changes = AnchorViewDeclaration.plan anchors history declaration
        apply numberOf countTokens (history @ changes) declaration messages

    let material numberOf entries =
        entries
        |> List.map (function
            | Kept message -> message.Id |> Option.map numberOf |> Option.defaultValue 0, message.Text
            | Summarized(message, summary) -> message.Id |> Option.map numberOf |> Option.defaultValue 0, summary
            | OpeningSummary summary -> 0, summary)

    /// A material identity includes its representative and exact rendered bytes.
    let materialKey (anchor: int) (text: string) =
        string anchor + ":" + string text.Length + ":" + text
