namespace Wanxiangshu.Context.Prefix.OpenCode

module XWireHostSurface =

    let suppressHostMessagesByIds (rawMessages: obj array) (hostMessageIds: string array) : obj array =
        XWire.suppressHostMessagesByIds
            (if isNull rawMessages then [] else Array.toList rawMessages)
            (if isNull hostMessageIds then
                 Set.empty
             else
                 Set.ofArray hostMessageIds)
        |> List.toArray
