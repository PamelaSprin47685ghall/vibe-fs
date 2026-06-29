module Wanxiangshu.Shell.ToolArgsDecode

open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.SubagentIntentsCodec
open Wanxiangshu.Shell.SubagentSimpleArgsCodec
open Wanxiangshu.Shell.WebToolsCodec
open Wanxiangshu.Shell.ExecutorToolsCodec
open Wanxiangshu.Shell.WorkBacklogToolsCodec
open Wanxiangshu.Shell.PatchToolsCodec
open Wanxiangshu.Shell.ReviewToolsCodec

let private mapRead (r: FileToolsCodec.ReadArgs) : Wanxiangshu.Kernel.ToolArgs.ReadArgs =
    { Path = r.Path; Offset = r.Offset; Limit = r.Limit }

let private mapWrite (w: FileToolsCodec.WriteArgs) : Wanxiangshu.Kernel.ToolArgs.WriteArgs =
    { FilePath = w.FilePath; Content = w.Content }

let private mapWebsearch (w: WebToolsCodec.WebsearchArgs) : Wanxiangshu.Kernel.ToolArgs.WebsearchArgs =
    { Query = w.Query; NumResults = w.NumResults; WhatToSummarize = w.WhatToSummarize }

let private mapWebfetch (w: WebToolsCodec.WebfetchArgs) : Wanxiangshu.Kernel.ToolArgs.WebfetchArgs =
    {
        Url = w.Url
        ExtractMain = w.ExtractMain
        PreferLlmsTxt = w.PreferLlmsTxt
        Prompt = w.Prompt
        Timeout = w.Timeout
    }

let private mapExecutor (e: ExecutorToolsCodec.ExecutorArgs) : Wanxiangshu.Kernel.ToolArgs.ExecutorArgs =
    {
        Language = e.Language
        Program = e.Program
        Dependencies = e.Dependencies
        TimeoutType = e.TimeoutType
        Mode = e.Mode
    }

let private mapTodoItem (t: WorkBacklogToolsCodec.TodoItem) : Wanxiangshu.Kernel.ToolArgs.TodoItem =
    { Content = t.Content; Status = t.Status; Priority = t.Priority }

let private mapTodoWrite (tw: WorkBacklogToolsCodec.TodoWriteArgs) : Wanxiangshu.Kernel.ToolArgs.TodoWriteArgs =
    {
        AhaMoments = tw.AhaMoments
        ChangesAndReasons = tw.ChangesAndReasons
        Gotchas = tw.Gotchas
        LessonsAndConventions = tw.LessonsAndConventions
        Plan = tw.Plan
        Todos = tw.Todos |> Array.map mapTodoItem
        SelectMethodology = tw.SelectMethodology
    }

type DecodedToolInvocation =
    | Typed of ToolArgs
    | CoderBatch of CoderIntent list
    | InvestigatorBatch of InvestigatorIntent list

let private intentsField (toolName: string) (args: obj) : Result<obj, DomainError> =
    let v = intentsRawFromArgs args
    if Dyn.isNullish v then Error (InvalidIntent (toolName, "intents", "required"))
    else Ok v

let decodeToolInvocation (toolName: string) (args: obj) : Result<DecodedToolInvocation, DomainError> =
    match toolName with
    | "read" ->
        FileToolsCodec.decodeReadArgs args
        |> Result.map (fun r -> Typed (ToolArgs.Read (mapRead r)))
    | "write" ->
        FileToolsCodec.decodeWriteArgs args
        |> Result.map (fun r -> Typed (ToolArgs.Write (mapWrite r)))
    | "coder" ->
        intentsField "coder" args
        |> Result.bind (fun raw ->
            parseCoderIntents raw
            |> Result.mapError (fun msg -> ParseError ("intents", msg))
            |> Result.map CoderBatch)
    | "investigator" ->
        intentsField "investigator" args
        |> Result.bind (fun raw ->
            parseInvestigatorIntents raw
            |> Result.mapError (fun msg -> ParseError ("intents", msg))
            |> Result.map InvestigatorBatch)
    | "meditator" ->
        decodeMeditatorArgs args
        |> Result.map (fun d -> Typed (Meditator { Intent = d.Intent; Files = d.Files }))
    | "browser" ->
        decodeBrowserArgs args
        |> Result.map (fun d -> Typed (Browser { Intent = d.Intent }))
    | "websearch" ->
        WebToolsCodec.decodeWebsearchArgs args
        |> Result.map (fun w -> Typed (ToolArgs.Websearch (mapWebsearch w)))
    | "webfetch" ->
        WebToolsCodec.decodeWebfetchArgs args
        |> Result.map (fun w -> Typed (ToolArgs.Webfetch (mapWebfetch w)))
    | "executor" ->
        ExecutorToolsCodec.decodeExecutorArgs args
        |> Result.map (fun e -> Typed (ToolArgs.Executor (mapExecutor e)))
    | "todowrite" ->
        decodeTodoWriteArgs args
        |> Result.map (fun tw -> Typed (ToolArgs.TodoWrite (mapTodoWrite tw)))
    | "apply_patch" ->
        decodeApplyPatchFields args
        |> Result.map (fun f -> Typed (ToolArgs.ApplyPatch { PatchText = f.PatchText }))
    | "submit_review" ->
        decodeSubmitReviewArgs args
        |> Result.map (fun sr -> Typed (ToolArgs.SubmitReview { Report = sr.Report; AffectedFiles = sr.AffectedFiles }))
    | _ -> Error (InvalidIntent (toolName, "tool", "unknown tool for ToolArgs decode"))

let decodeToolArgs (toolName: string) (args: obj) : Result<ToolArgs, DomainError> =
    match decodeToolInvocation toolName args with
    | Error e -> Error e
    | Ok (Typed ta) -> Ok ta
    | Ok (CoderBatch _) | Ok (InvestigatorBatch _) ->
        Error (InvalidIntent (toolName, "tool", "subagent intents are not ToolArgs"))