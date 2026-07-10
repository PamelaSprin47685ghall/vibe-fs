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
      Program: string
      Dependencies: string list
      TimeoutType: ExecutorTimeoutType
      Mode: string
      WhatToSummarize: string }

type TodoItemStatus =
    | Todo
    | InProgress
    | Completed
    | Cancelled

type TodoItemPriority =
    | Low
    | Medium
    | High

type TodoItem =
    { Content: string
      Status: TodoItemStatus
      Priority: TodoItemPriority }

type TodoWriteArgs =
    { AhaMoments: string
      ChangesAndReasons: string
      Gotchas: string
      LessonsAndConventions: string
      Plan: string
      Todos: TodoItem array
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
