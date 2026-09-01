namespace Wanxiangshu.OpenCode

[<RequireQualifiedAccess>]
type MessagePart =
    | Text of text: string
    | Reasoning of text: string
    | ToolCall of callId: string * name: string * argsCanonical: string
    | ToolResult of callId: string * resultCanonical: string
    | Activity of kind: string

module HostMessageCodec =
    val decodePart: raw: obj -> MessagePart option
    val decodeParts: rawParts: obj array -> MessagePart array
