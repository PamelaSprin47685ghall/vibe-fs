namespace Wanxiangshu.Repository.Programming.Js.Anchor

[<RequireQualifiedAccess>]
module AnchorView =
    let report
        (countTokens: string -> int)
        (source: AnchorMessage list)
        (protectedPrefix: AnchorSegment list)
        (segments: AnchorSegment list)
        =
        let messages = segments |> List.collect _.Messages
        let isLiteral (message: AnchorMessage) = isNull message.Raw

        { Total = source |> List.sumBy (fun message -> countTokens message.Text)
          Used = messages |> List.sumBy (fun message -> countTokens message.Text)
          Cached =
            protectedPrefix
            |> List.collect _.Messages
            |> List.sumBy (fun message -> countTokens message.Text)
          Kept = messages |> List.filter (isLiteral >> not) |> List.length
          Inserted = messages |> List.filter isLiteral |> List.length
          Dropped = max 0 (List.length source - List.length messages)
          PrefixCount = protectedPrefix.Length }
