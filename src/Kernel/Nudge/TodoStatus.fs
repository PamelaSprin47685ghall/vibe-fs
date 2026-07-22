module Wanxiangshu.Kernel.Nudge.TodoStatus

open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel

/// Terminal todo statuses that should NOT count as open work.
type TodoStatus =
    | Completed
    | Cancelled
    | Abandoned
    | InProgress
    | Pending

let fromTodoItemStatus (s: Wanxiangshu.Kernel.ToolArgs.TodoItemStatus) : TodoStatus =
    match s with
    | Wanxiangshu.Kernel.ToolArgs.Todo -> Pending
    | Wanxiangshu.Kernel.ToolArgs.InProgress -> InProgress
    | Wanxiangshu.Kernel.ToolArgs.Completed -> Completed
    | Wanxiangshu.Kernel.ToolArgs.Cancelled -> Cancelled

let todoStatusOfString (s: string) : TodoStatus option =
    match s.ToLowerInvariant() with
    | "completed" -> Some Completed
    | "cancelled" -> Some Cancelled
    | "abandoned" -> Some Abandoned
    | "in_progress"
    | "inprogress" -> Some InProgress
    | "pending" -> Some Pending
    | _ -> None

let isTerminal (s: TodoStatus) : bool =
    match s with
    | Completed
    | Cancelled
    | Abandoned -> true
    | InProgress
    | Pending -> false

let isTerminalAssistantFinish (finish: string) : bool =
    match FinishReason.fromString finish with
    | FinishReason.ToolCalls
    | FinishReason.ToolUseError
    | FinishReason.Abort
    | FinishReason.Interrupted
    | FinishReason.Cancelled -> false
    | _ -> true

let syntheticAssistantAgents: Set<string> = Set.ofList [ "compaction"; "title" ]

let isSyntheticAssistantAgent (agent: string) : bool =
    Set.contains (agent.Trim().ToLowerInvariant()) syntheticAssistantAgents
