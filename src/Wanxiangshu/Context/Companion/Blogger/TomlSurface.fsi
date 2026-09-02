namespace Wanxiangshu.Context.Companion.Blogger

/// JS-native semantic surface for Blogger delta TOML rendering (CTX-013,
/// P6 wave). Delta parts cross as JSON-shaped discriminated values
/// ({ kind: 'text', text } / { kind: 'toolCall', tool, args } / ...);
/// translation to the F# BloggerDeltaPart union happens here at the owner
/// boundary. The renderer core stays untouched (JS-SEMANTIC-SURFACE-003/005).
module BloggerTomlSurface =

    val TruncationMarker: string
    val DoNotExecTable: string
    val NewWorkTable: string

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

    /// One rendered part: role + JSON part + truncated flag.
    val renderItem: item: ItemInput -> string
    val renderHistoricFrame: frameBody: string -> string
    val renderPreviousEnforcerTip: tipField: string -> cycleId: string -> string
    val renderWith: instructions: string array -> items: ItemInput array -> string
    val render: items: ItemInput array -> string
