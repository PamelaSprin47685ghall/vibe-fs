namespace Wanxiangshu.Context.Companion.Blogger

open Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type BloggerDeltaPart =
    | TextPart of text: string
    | ReasoningPart of text: string
    | ToolCallPart of tool: string * canonicalArgs: string
    | ToolResultPart of text: string
    | ImageOmitted of mediaType: string option
    | MediaOmitted of mediaType: string option

type BloggerDeltaItem =
    { Role: string
      Part: BloggerDeltaPart
      Truncated: bool }

[<RequireQualifiedAccess>]
module BloggerToml =
    val TruncationMarker: string
    val DoNotExecTable: string
    val NewWorkTable: string
    val dataBlock: item: BloggerDeltaItem -> LlmFacing.DataBlock
    val renderItem: item: BloggerDeltaItem -> string
    val renderHistoricFrame: frameBody: string -> string
    val renderPreviousEnforcerTip: tipField: string -> cycleId: string -> string
    val documentWith: instructions: string list -> items: BloggerDeltaItem list -> LlmFacing.Document
    val renderWith: instructions: string list -> items: BloggerDeltaItem list -> string
    val render: items: BloggerDeltaItem list -> string
