namespace Wanxiangshu.Context.Companion.Blogger

open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling

/// Stable semantic wire for carrying a Blogger delta item through durable
/// request materialization. This is deliberately NOT rendered TOML: the item
/// stays typed until the provider projection performs its one final render.
type BloggerDeltaItemWire =
    { Role: string
      Kind: string
      Text: string option
      Tool: string option
      Args: string option
      MediaType: string option
      Truncated: bool }

[<RequireQualifiedAccess>]
module BloggerDeltaItemWire =

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    let private optionalString (value: obj) =
        if isNullish value then None else Some(string value)

    let private stringOrEmpty (value: obj) =
        if isNullish value then "" else string value

    let private boolOrFalse (value: obj) =
        if isNullish value then false else unbox<bool> value

    let ofItem (item: BloggerDeltaItem) : BloggerDeltaItemWire =
        let kind, text, tool, args, mediaType =
            match item.Part with
            | BloggerDeltaPart.TextPart value -> "text", Some value, None, None, None
            | BloggerDeltaPart.ReasoningPart value -> "reasoning", Some value, None, None, None
            | BloggerDeltaPart.ToolCallPart(tool, args) -> "tool-call", None, Some tool, Some args, None
            | BloggerDeltaPart.ToolResultPart value -> "tool-result", Some value, None, None, None
            | BloggerDeltaPart.ImageOmitted mediaType -> "image-omitted", None, None, None, mediaType
            | BloggerDeltaPart.MediaOmitted mediaType -> "media-omitted", None, None, None, mediaType

        { Role = item.Role
          Kind = kind
          Text = text
          Tool = tool
          Args = args
          MediaType = mediaType
          Truncated = item.Truncated }

    let private toolCallPart tool args =
        match tool, args with
        | Some toolName, Some canonicalArgs -> Ok(BloggerDeltaPart.ToolCallPart(toolName, canonicalArgs))
        | _ -> Error "blogger delta tool-call wire requires tool and args"

    let tryToItem (wire: BloggerDeltaItemWire) : Result<BloggerDeltaItem, string> =
        let text () = wire.Text |> Option.defaultValue ""

        let part =
            match wire.Kind with
            | "text" -> Ok(BloggerDeltaPart.TextPart(text ()))
            | "reasoning" -> Ok(BloggerDeltaPart.ReasoningPart(text ()))
            | "tool-call" -> toolCallPart wire.Tool wire.Args
            | "tool-result" -> Ok(BloggerDeltaPart.ToolResultPart(text ()))
            | "image-omitted" -> Ok(BloggerDeltaPart.ImageOmitted wire.MediaType)
            | "media-omitted" -> Ok(BloggerDeltaPart.MediaOmitted wire.MediaType)
            | other -> Error(sprintf "unknown blogger delta item kind: %s" other)

        part
        |> Result.map (fun value ->
            { Role = wire.Role
              Part = value
              Truncated = wire.Truncated })

    let internal toJs (item: BloggerDeltaItem) : obj =
        let wire = ofItem item

        createObj
            [ "role", box wire.Role
              "kind", box wire.Kind
              "text", box (wire.Text |> Option.toObj)
              "tool", box (wire.Tool |> Option.toObj)
              "args", box (wire.Args |> Option.toObj)
              "mediaType", box (wire.MediaType |> Option.toObj)
              "truncated", box wire.Truncated ]

    let internal tryOfJs (value: obj) : Result<BloggerDeltaItem, string> =
        if isNullish value then
            Error "blogger delta item wire is null"
        else
            let wire =
                { Role = stringOrEmpty value?role
                  Kind = stringOrEmpty value?kind
                  Text = optionalString value?text
                  Tool = optionalString value?tool
                  Args = optionalString value?args
                  MediaType = optionalString value?mediaType
                  Truncated = boolOrFalse value?truncated }

            tryToItem wire

    let internal tryListOfJs (value: obj) : Result<BloggerDeltaItem list, string> =
        if isNullish value then
            Ok []
        elif not (emitJsExpr value "Array.isArray($0)") then
            Error "blogger delta items must be an array"
        else
            unbox<obj array> value |> Array.toList |> List.traverseResultM tryOfJs
