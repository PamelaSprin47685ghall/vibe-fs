module Wanxiangshu.Kernel.Backlog.BacklogTypes

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ToolExecutionStatusModule

let todoWriteToolNameFor (host: Host) : string = todoWriteToolName host
let todoWriteToolNameDefault = todoWriteToolNameFor opencode
let reviewToolName = "submit_review"

type BacklogEntry =
    { ahaMoments: string
      changesAndReasons: string
      gotchas: string
      lessonsAndConventions: string
      plan: string }

let trunc (s: string) : string = if s = null then "" else s.Trim()

let isTodoResultFor (host: Host) (part: Part<'raw>) : bool =
    match part with
    | ToolPart(toolName, _, Some state, _) when
        toolName = todoWriteToolNameFor host
        && state.status = ToolExecutionStatus.Completed
        ->
        true
    | _ -> false

let isTodoResult (part: Part<'raw>) : bool = isTodoResultFor opencode part

let isTodoErrorFor (host: Host) (part: Part<'raw>) : bool =
    match part with
    | ToolPart(toolName, _, Some state, _) when
        toolName = todoWriteToolNameFor host && state.status = ToolExecutionStatus.Error
        ->
        true
    | _ -> false

let isTodoError (part: Part<'raw>) : bool = isTodoErrorFor opencode part

let lastTodoErrorTextFor (host: Host) (flat: FlatPart<'raw> list) : string option =
    flat
    |> List.tryFindBack (fun fp -> isTodoErrorFor host fp.part)
    |> Option.map (fun fp ->
        match fp.part with
        | ToolPart(_, _, Some state, _) -> state.error
        | _ -> "")

let isReviewTool (part: Part<'raw>) : bool =
    match part with
    | ToolPart(toolName, _, _, _) when toolName = reviewToolName -> true
    | _ -> false

let lastTodoErrorText (flat: FlatPart<'raw> list) : string option = lastTodoErrorTextFor opencode flat
