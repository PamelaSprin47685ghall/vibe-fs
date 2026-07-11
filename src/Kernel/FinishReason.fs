namespace Wanxiangshu.Kernel

[<RequireQualifiedAccess>]
type FinishReason =
    | Stop
    | End
    | ToolCalls
    | ToolUseError
    | Abort
    | Interrupted
    | Cancelled
    | QueuedMessage
    | Unknown of string

module FinishReason =

    let fromString (s: string) : FinishReason =
        if isNull s then
            FinishReason.Unknown ""
        else
            let trimmed = s.Trim()

            match trimmed.ToLowerInvariant() with
            | "stop" -> FinishReason.Stop
            | "end" -> FinishReason.End
            | "tool" -> FinishReason.ToolCalls
            | "tool_calls" -> FinishReason.ToolCalls
            | "tool-calls" -> FinishReason.ToolCalls
            | "tool_use_error" -> FinishReason.ToolUseError
            | "tool-use-error" -> FinishReason.ToolUseError
            | "tool_use" -> FinishReason.ToolCalls
            | "tool-use" -> FinishReason.ToolCalls
            | "abort" -> FinishReason.Abort
            | "tool_abort" -> FinishReason.Abort
            | "tool-abort" -> FinishReason.Abort
            | "interrupted" -> FinishReason.Interrupted
            | "cancelled" -> FinishReason.Cancelled
            | "queued-message" -> FinishReason.QueuedMessage
            | "queued_message" -> FinishReason.QueuedMessage
            | _ -> FinishReason.Unknown trimmed

    let toString (reason: FinishReason) : string =
        match reason with
        | FinishReason.Stop -> "stop"
        | FinishReason.End -> "end"
        | FinishReason.ToolCalls -> "tool_calls"
        | FinishReason.ToolUseError -> "tool_use_error"
        | FinishReason.Abort -> "abort"
        | FinishReason.Interrupted -> "interrupted"
        | FinishReason.Cancelled -> "cancelled"
        | FinishReason.QueuedMessage -> "queued_message"
        | FinishReason.Unknown s -> s

    let isTerminal (reason: FinishReason) : bool =
        match reason with
        | FinishReason.Stop
        | FinishReason.End -> true
        | _ -> false

    let isToolFinish (reason: FinishReason) : bool =
        match reason with
        | FinishReason.ToolCalls
        | FinishReason.ToolUseError -> true
        | _ -> false

    let isAbort (reason: FinishReason) : bool =
        match reason with
        | FinishReason.Abort
        | FinishReason.Interrupted
        | FinishReason.Cancelled -> true
        | _ -> false
