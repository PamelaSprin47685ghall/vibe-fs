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

    type private Row =
        { Position: int
          Entry: AnchorViewEntry }

    let private textOf =
        function
        | Kept message -> message.Text
        | Summarized(_, summary)
        | OpeningSummary summary -> summary

    let private messageOf =
        function
        | Kept message
        | Summarized(message, _) -> message
        | OpeningSummary _ -> invalidOp "a real anchor cannot replace the opening row"

    let private replace (rows: Row list) (change: AnchorReplacement) =
        let before = rows |> List.takeWhile (fun row -> row.Position < change.Anchor)
        let remaining = rows |> List.skip before.Length

        let covered, after =
            remaining |> List.partition (fun row -> row.Position < change.Until)

        match change.Anchor, covered with
        | 0, _ ->
            before
            @ [ { Position = 0
                  Entry = OpeningSummary change.Summary } ]
            @ after
        | _, [] -> rows
        | _, first :: _ ->
            let message = messageOf first.Entry

            before
            @ [ { Position = change.Anchor
                  Entry = Summarized(message, change.Summary) } ]
            @ after

    let apply
        (numberOf: string -> int)
        (countTokens: string -> int)
        (history: AnchorReplacement list)
        (declaration: AnchorViewDeclaration)
        (messages: AnchorMessage list)
        : AnchorViewEntry list * AnchorViewReport =
        let _, rows =
            messages
            |> List.mapFold
                (fun position message ->
                    let anchor = message.Id |> Option.map numberOf |> Option.defaultValue position

                    { Position = anchor
                      Entry = Kept message },
                    anchor)
                0
            |> fun (rows, position) -> position, rows

        let projected = history |> List.fold replace rows
        let entries = projected |> List.map _.Entry

        let kept =
            entries
            |> List.sumBy (function
                | Kept _ -> 1
                | _ -> 0)

        let summarized = entries.Length - kept

        let opening =
            entries
            |> List.sumBy (function
                | OpeningSummary _ -> 1
                | _ -> 0)

        entries,
        { Total = messages |> List.sumBy (fun message -> countTokens message.Text)
          Used = entries |> List.sumBy (textOf >> countTokens)
          Cached =
            projected
            |> List.sumBy (fun row ->
                if row.Position < declaration.PrefixUntil then
                    countTokens (textOf row.Entry)
                else
                    0)
          Kept = kept
          Summarized = summarized
          Folded = max 0 (messages.Length - entries.Length + opening)
          Replaced = history |> List.map _.Anchor |> List.distinct |> List.sort }

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
