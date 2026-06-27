module Wanxiangshu.Kernel.ToolArgs

open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Kernel.KnowledgeGraph
open Wanxiangshu.Kernel.KnowledgeGraph.Types

type ReadArgs = { Path: string; Offset: int option; Limit: int option }

type WriteArgs = { FilePath: string; Content: string }

type MeditatorArgs = { Intent: string; Files: string array }

type BrowserArgs = { Intent: string }

type WebsearchArgs = {
    Query: string
    NumResults: int
    WhatToSummarize: string
}

type WebfetchArgs = {
    Url: string
    ExtractMain: bool option
    PreferLlmsTxt: string option
    Prompt: string option
    Timeout: int option
}

type ExecutorArgs = {
    Language: ExecutorLanguage
    Program: string
    Dependencies: string list
    TimeoutType: ExecutorTimeoutType
    Mode: string
    Warn: ExecutorWarn
}

type TodoItem = { Content: string; Status: string; Priority: string }

type TodoWriteArgs = {
    CompletedWorkReport: string
    Todos: TodoItem array
    SelectMethodology: string list
}

type KnowledgeGraphFetchArgs = { Entity: string }

type ApplyPatchArgs = { PatchText: string }

type SubmitReviewArgs = { Report: string; AffectedFiles: string list }

type ToolArgs =
    | Read of ReadArgs
    | Write of WriteArgs
    | Meditator of MeditatorArgs
    | Browser of BrowserArgs
    | Websearch of WebsearchArgs
    | Webfetch of WebfetchArgs
    | Executor of ExecutorArgs
    | TodoWrite of TodoWriteArgs
    | KnowledgeGraphFetch of KnowledgeGraphFetchArgs
    | ReturnBookkeeper of KnowledgeGraphDraft list
    | ApplyPatch of ApplyPatchArgs
    | SubmitReview of SubmitReviewArgs