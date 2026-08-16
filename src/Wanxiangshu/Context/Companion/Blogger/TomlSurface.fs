namespace Wanxiangshu.Context.Companion.Blogger

/// JS-native semantic surface for Blogger delta TOML rendering (CTX-013,
/// P6 wave). Delta parts cross as JSON-shaped discriminated values
/// ({ kind: 'text', text } / { kind: 'toolCall', tool, args } / ...);
/// translation to the F# BloggerDeltaPart union happens here at the owner
/// boundary. The renderer core stays untouched (JS-SEMANTIC-SURFACE-003/005).
module BloggerTomlSurface =

    let TruncationMarker = BloggerToml.TruncationMarker
    let DoNotExecTable = BloggerToml.DoNotExecTable
    let NewWorkTable = BloggerToml.NewWorkTable

    type PartInput =
        {| Kind: string
           Text: string
           Tool: string
           Args: string
           MediaType: string |}

    type ItemInput =
        {| Role: string
           Part: PartInput
           Truncated: bool |}

    let private partOf (p: PartInput) : BloggerDeltaPart =
        match p.Kind with
        | "text" -> BloggerDeltaPart.TextPart p.Text
        | "reasoning" -> BloggerDeltaPart.ReasoningPart p.Text
        | "toolCall" -> BloggerDeltaPart.ToolCallPart(p.Tool, p.Args)
        | "toolResult" -> BloggerDeltaPart.ToolResultPart p.Text
        | "imageOmitted" ->
            BloggerDeltaPart.ImageOmitted(
                if isNull p.MediaType || p.MediaType = "" then
                    None
                else
                    Some p.MediaType
            )
        | "mediaOmitted" ->
            BloggerDeltaPart.MediaOmitted(
                if isNull p.MediaType || p.MediaType = "" then
                    None
                else
                    Some p.MediaType
            )
        | other -> failwithf "BloggerTomlSurface: unknown part kind %s" other

    let private itemOf (item: ItemInput) =
        { Role = item.Role
          Part = partOf item.Part
          Truncated = item.Truncated }

    /// One rendered part: role + JSON part + truncated flag.
    let renderItem (item: ItemInput) : string = BloggerToml.renderItem (itemOf item)

    let renderHistoricFrame (frameBody: string) : string =
        BloggerToml.renderHistoricFrame frameBody

    let renderPreviousEnforcerTip (tipField: string) (cycleId: string) : string =
        BloggerToml.renderPreviousEnforcerTip tipField cycleId

    let renderWith (instructions: string array) (items: ItemInput array) : string =
        BloggerToml.renderWith (List.ofArray instructions) (Array.toList (Array.map itemOf items))

    let render (items: ItemInput array) : string =
        BloggerToml.render (Array.toList (Array.map itemOf items))
