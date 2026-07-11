module Wanxiangshu.Kernel.ToolExecutionStatusModule

[<RequireQualifiedAccess>]
type ToolExecutionStatus =
    | Completed
    | Error
    | Pending
    | Unknown of string

let fromString (s: string) : ToolExecutionStatus =
    if s = null then
        ToolExecutionStatus.Unknown ""
    else
        match s.Trim().ToLowerInvariant() with
        | "completed" -> ToolExecutionStatus.Completed
        | "error" -> ToolExecutionStatus.Error
        | "pending" -> ToolExecutionStatus.Pending
        | other -> ToolExecutionStatus.Unknown other

let toString (status: ToolExecutionStatus) : string =
    match status with
    | ToolExecutionStatus.Completed -> "completed"
    | ToolExecutionStatus.Error -> "error"
    | ToolExecutionStatus.Pending -> "pending"
    | ToolExecutionStatus.Unknown other -> other
