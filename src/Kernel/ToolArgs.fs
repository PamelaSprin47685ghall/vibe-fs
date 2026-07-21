module Wanxiangshu.Kernel.ToolArgs

open Wanxiangshu.Kernel.Executor

type ReadArgs =
    { Path: string
      Offset: int option
      Limit: int option }

type WriteArgs = { FilePath: string; Content: string }

type BrowserArgs = { Intent: string }

type ContinueArgs = { Iterator: string; Prompt: string }

type WebsearchArgs =
    { Query: string
      NumResults: int
      WhatToSummarize: string }

type WebfetchArgs =
    { Url: string
      ExtractMain: bool option
      PreferLlmsTxt: string option
      Prompt: string option
      Timeout: int option }

type ExecutorArgs =
    { Language: ExecutorLanguage
      Command: string
      Dependencies: string list
      TimeoutType: ExecutorTimeoutType
      WhatToSummarize: string }

type TodoItemStatus =
    | Todo
    | InProgress
    | Completed
    | Cancelled

module TodoItemStatus =
    let fromString (s: string) : TodoItemStatus =
        if isNull s then
            Todo
        else
            match s.Trim().ToLowerInvariant() with
            | "completed" -> Completed
            | "cancelled"
            | "canceled" -> Cancelled
            | "in_progress"
            | "inprogress" -> InProgress
            | _ -> Todo

    let isTerminal (s: TodoItemStatus) : bool =
        match s with
        | Completed
        | Cancelled -> true
        | Todo
        | InProgress -> false

type TodoItemPriority =
    | Low
    | Medium
    | High

type TodoItem =
    { Content: string
      Status: TodoItemStatus
      Priority: TodoItemPriority }

type TodoWriteArgs =
    { Todos: TodoItem array
      SelectMethodology: string list }

type ApplyPatchArgs = { PatchText: string }

type SubmitReviewArgs =
    { Report: string
      AffectedFiles: string list }

type ToolArgs =
    | Read of ReadArgs
    | Write of WriteArgs
    | Browser of BrowserArgs
    | Continue of ContinueArgs
    | Websearch of WebsearchArgs
    | Webfetch of WebfetchArgs
    | Executor of ExecutorArgs
    | TodoWrite of TodoWriteArgs
    | ApplyPatch of ApplyPatchArgs
    | SubmitReview of SubmitReviewArgs
