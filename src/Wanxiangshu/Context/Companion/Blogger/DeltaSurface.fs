// primary_owner: context-compression — Context.BloggerSurface (CONTEXT-COMPRESSION-012) — KEEP — sole delta contract
namespace Wanxiangshu.Context.Companion.Blogger

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Foundation

/// Context-compression delta owner. Semantic message parts enter as plain
/// objects; the BloggerDelta cursor/chunker and Fable unions stay private.
[<RequireQualifiedAccess>]
module BloggerDeltaSurface =

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    let private textValue (value: obj) : string =
        if isNullish value then "" else string value

    let private optionalText (value: obj) : string option =
        if isNullish value then None else Some(textValue value)

    let private intValue (value: obj) : int = int (textValue value)

    let private partOfJs (value: obj) : SemanticPart =
        match textValue value?kind with
        | "reasoning"
        | "ReasoningPart" -> SemanticReasoning(textValue value?text)
        | "tool-call"
        | "ToolCallPart" -> SemanticToolCall(textValue value?tool, textValue value?args)
        | "tool-result"
        | "ToolResultPart" -> SemanticToolResult(textValue value?text)
        | "media"
        | "ImageOmitted"
        | "MediaOmitted" -> SemanticMedia(optionalText value?mediaType, textValue value?digest)
        | _ -> SemanticText(textValue value?text)

    let private messagesOfJs (value: obj) : SemanticMessage list =
        let messages = if isNullish value then [||] else unbox<obj array> value

        messages
        |> Array.toList
        |> List.map (fun message ->
            let parts =
                if isNullish message?parts then
                    [||]
                else
                    unbox<obj array> message?parts

            { Role = textValue message?role
              Parts = parts |> Array.toList |> List.map partOfJs })

    let private itemKind (part: BloggerDeltaPart) : string =
        match part with
        | BloggerDeltaPart.TextPart _ -> "TextPart"
        | BloggerDeltaPart.ReasoningPart _ -> "ReasoningPart"
        | BloggerDeltaPart.ToolCallPart _ -> "ToolCallPart"
        | BloggerDeltaPart.ToolResultPart _ -> "ToolResultPart"
        | BloggerDeltaPart.ImageOmitted _ -> "ImageOmitted"
        | BloggerDeltaPart.MediaOmitted _ -> "MediaOmitted"

    let private itemToJs (item: BloggerDeltaItem) : obj =
        let partFields =
            match item.Part with
            | BloggerDeltaPart.TextPart value -> box {| text = value |}
            | BloggerDeltaPart.ReasoningPart value -> box {| text = value |}
            | BloggerDeltaPart.ToolCallPart(tool, args) -> box {| tool = tool; args = args |}
            | BloggerDeltaPart.ToolResultPart value -> box {| text = value |}
            | BloggerDeltaPart.ImageOmitted mediaType
            | BloggerDeltaPart.MediaOmitted mediaType -> box {| mediaType = mediaType |> Option.toObj |}

        box
            {| kind = itemKind item.Part
               role = item.Role
               truncated = item.Truncated
               fields = partFields |}

    let private cursorToJs (cursor: SemanticCursor) : obj =
        box
            {| turn = cursor.TurnIndex
               part = cursor.PartIndex |}

    /// Plain constructors used by semantic tests and other JS callers.
    let textPart (value: string) : obj = box {| kind = "text"; text = value |}

    let reasoningPart (value: string) : obj =
        box {| kind = "reasoning"; text = value |}

    let toolCallPart (tool: string) (args: string) : obj =
        box
            {| kind = "tool-call"
               tool = tool
               args = args |}

    let toolResultPart (value: string) : obj =
        box {| kind = "tool-result"; text = value |}

    let mediaPart (mediaType: obj) (digest: string) : obj =
        box
            {| kind = "media"
               mediaType = mediaType
               digest = digest |}

    let text (value: string) : obj = textPart value
    let reasoning (value: string) : obj = reasoningPart value
    let toolCall (tool: string) (args: string) : obj = toolCallPart tool args
    let toolResult (value: string) : obj = toolResultPart value
    let media (mediaType: obj) (digest: string) : obj = mediaPart mediaType digest
    let byteCount (value: string) : int = LlmFacing.byteCount value
    let messages (values: obj array) : obj = box values

    let cursor (turn: int) (part: int) : obj = box {| turn = turn; part = part |}

    let limitBytes = BloggerDelta.DeltaLimitBytes

    /// Render the next deterministic chunk. `None` remains an absent option at
    /// the JS boundary; a present chunk is a plain semantic summary.
    let nextChunk (request: obj) : obj option =
        let cursorValue = request?cursor

        let cursor =
            { TurnIndex = intValue cursorValue?turn
              PartIndex = intValue cursorValue?part }

        let previousCutoff =
            if isNullish request?previousCutoff then
                0
            else
                intValue request?previousCutoff

        BloggerDelta.nextChunk (intValue request?limit) cursor previousCutoff (messagesOfJs request?messages)
        |> Option.map (fun chunk ->
            let bytes = LlmFacing.byteCount chunk.Toml
            let items = chunk.Items |> List.map itemToJs |> List.toArray

            box
                {| items = items
                   kinds = chunk.Items |> List.map (fun item -> itemKind item.Part) |> List.toArray
                   truncatedFlags = chunk.Items |> List.map (fun item -> item.Truncated) |> List.toArray
                   itemCount = List.length chunk.Items
                   bytes = bytes
                   toml = chunk.Toml
                   nextCursor = cursorToJs chunk.NextCursor
                   nextCutoff = chunk.NextCoverableTurnCutoffExclusive |})
