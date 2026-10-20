namespace Wanxiangshu.Repository.Programming.Js.Anchor

type AnchorViewDeclaration =
    { PrefixUntil: int
      ThenMap: (int * string option) list }

type AnchorReplacement =
    { Anchor: int
      Until: int
      Summary: string }

[<RequireQualifiedAccess>]
module AnchorViewDeclaration =
    let empty = { PrefixUntil = 0; ThenMap = [] }

    let live anchors history =
        let removed = history |> List.map _.Anchor |> Set.ofList

        0
        :: (anchors
            |> List.filter (fun anchor -> anchor > 0 && not (Set.contains anchor removed)))
        |> List.distinct
        |> List.sort

    let plan anchors history declaration =
        let surviving = live anchors history
        let endOfInput = (anchors |> List.fold max 0) + 1

        declaration.ThenMap
        |> List.choose (fun (anchor, summary) ->
            if anchor < declaration.PrefixUntil || not (List.contains anchor surviving) then
                None
            else
                summary
                |> Option.map (fun text ->
                    { Anchor = anchor
                      Until =
                        surviving
                        |> List.tryFind (fun candidate -> candidate > anchor)
                        |> Option.defaultValue endOfInput
                      Summary = text }))
        |> List.sortBy _.Anchor
