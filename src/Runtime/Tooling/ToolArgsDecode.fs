module Wanxiangshu.Runtime.ToolArgsDecode

open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.SubagentIntentsCodec
open Wanxiangshu.Runtime.SubagentSimpleArgsCodec
open Wanxiangshu.Runtime.WebToolsCodec
open Wanxiangshu.Runtime.ExecutorToolsCodec
open Wanxiangshu.Runtime.WorkBacklogToolsCodec
open Wanxiangshu.Runtime.PatchToolsCodec
open Wanxiangshu.Runtime.ReviewToolsCodec

let private mapRead (r: FileToolsCodec.ReadArgs) : Wanxiangshu.Kernel.ToolArgs.ReadArgs =
    { Path = r.Path
      Offset = r.Offset
      Limit = r.Limit }

let private mapWrite (w: FileToolsCodec.WriteArgs) : Wanxiangshu.Kernel.ToolArgs.WriteArgs =
    { FilePath = w.FilePath
      Content = w.Content }

let private mapWebsearch (w: WebToolsCodec.WebsearchArgs) : Wanxiangshu.Kernel.ToolArgs.WebsearchArgs =
    { Query = w.Query
      NumResults = w.NumResults
      WhatToSummarize = w.WhatToSummarize }

let private mapWebfetch (w: WebToolsCodec.WebfetchArgs) : Wanxiangshu.Kernel.ToolArgs.WebfetchArgs =
    { Url = w.Url
      ExtractMain = w.ExtractMain
      PreferLlmsTxt = w.PreferLlmsTxt
      Prompt = w.Prompt
      Timeout = w.Timeout }

let private mapExecutor (e: ExecutorToolsCodec.ExecutorArgs) : Wanxiangshu.Kernel.ToolArgs.ExecutorArgs =
    { Language = e.Language
      Command = e.Command
      Dependencies = e.Dependencies
      TimeoutType = e.TimeoutType
      Mode = e.Mode
      WhatToSummarize = e.WhatToSummarize }

let private mapTodoItem (t: WorkBacklogToolsCodec.TodoItem) : Wanxiangshu.Kernel.ToolArgs.TodoItem =
    { Content = t.Content
      Status = t.Status
      Priority = t.Priority }

let private mapTodoWrite (tw: WorkBacklogToolsCodec.TodoWriteArgs) : Wanxiangshu.Kernel.ToolArgs.TodoWriteArgs =
    { AhaMoments = tw.AhaMoments
      ChangesAndReasons = tw.ChangesAndReasons
      Gotchas = tw.Gotchas
      LessonsAndConventions = tw.LessonsAndConventions
      Plan = tw.Plan
      Todos = tw.Todos |> Array.map mapTodoItem
      SelectMethodology = tw.SelectMethodology }

type DecodedToolInvocation =
    | Typed of ToolArgs
    | CoderBatch of CoderIntent list
    | InvestigatorBatch of InvestigatorIntent list

let private intentsField (toolName: string) (args: obj) : Result<obj, DomainError> =
    let v = intentsRawFromArgs args

    if Dyn.isNullish v then
        Error(InvalidIntent(toolName, "intents", "required"))
    else
        Ok v

let private decodeRead args =
    FileToolsCodec.decodeReadArgs args
    |> Result.map (fun r -> Typed(ToolArgs.Read(mapRead r)))

let private decodeWrite args =
    FileToolsCodec.decodeWriteArgs args
    |> Result.map (fun r -> Typed(ToolArgs.Write(mapWrite r)))

let private decodeCoder args =
    intentsField "coder" args
    |> Result.bind (fun raw ->
        parseCoderIntents raw
        |> Result.mapError (fun msg -> ParseError("intents", msg))
        |> Result.map CoderBatch)

let private decodeInvestigator args =
    intentsField "investigator" args
    |> Result.bind (fun raw ->
        parseInvestigatorIntents raw
        |> Result.mapError (fun msg -> ParseError("intents", msg))
        |> Result.map InvestigatorBatch)

let private decodeBrowser args =
    decodeBrowserArgs args
    |> Result.map (fun d -> Typed(Browser { Intent = d.Intent }))

let private decodeContinue args =
    decodeContinueArgs args
    |> Result.map (fun d ->
        Typed(
            Continue
                { Iterator = d.Iterator
                  Prompt = d.Prompt }
        ))

let private decodeWebsearch args =
    WebToolsCodec.decodeWebsearchArgs args
    |> Result.map (fun w -> Typed(ToolArgs.Websearch(mapWebsearch w)))

let private decodeWebfetch args =
    WebToolsCodec.decodeWebfetchArgs args
    |> Result.map (fun w -> Typed(ToolArgs.Webfetch(mapWebfetch w)))

let private decodeExecutor args =
    ExecutorToolsCodec.decodeExecutorArgs args
    |> Result.map (fun e -> Typed(ToolArgs.Executor(mapExecutor e)))

let private decodeTodoWrite (originalToolName: string) args =
    decodeTodoWriteArgs (originalToolName = "task") args
    |> Result.map (fun (tw, _) -> Typed(ToolArgs.TodoWrite(mapTodoWrite tw)))

let private decodeApplyPatch args =
    decodeApplyPatchFields args
    |> Result.map (fun f -> Typed(ToolArgs.ApplyPatch { PatchText = f.PatchText }))

let private decodeSubmitReview args =
    decodeSubmitReviewArgs args
    |> Result.map (fun sr ->
        Typed(
            ToolArgs.SubmitReview
                { Report = sr.Report
                  AffectedFiles = sr.AffectedFiles }
        ))

let decodeToolInvocation (toolName: string) (args: obj) : Result<DecodedToolInvocation, DomainError> =
    let cleanToolName = if toolName = "continue" then "continue" else toolName

    match cleanToolName with
    | "read" -> decodeRead args
    | "write" -> decodeWrite args
    | "coder" -> decodeCoder args
    | "investigator" -> decodeInvestigator args
    | "browser" -> decodeBrowser args
    | "continue" -> decodeContinue args
    | "websearch" -> decodeWebsearch args
    | "webfetch" -> decodeWebfetch args
    | "executor" -> decodeExecutor args
    | "todowrite" -> decodeTodoWrite toolName args
    | "apply_patch" -> decodeApplyPatch args
    | "submit_review" -> decodeSubmitReview args
    | _ -> Error(InvalidIntent(toolName, "tool", "unknown tool for ToolArgs decode"))

let decodeToolArgs (toolName: string) (args: obj) : Result<ToolArgs, DomainError> =
    match decodeToolInvocation toolName args with
    | Error e -> Error e
    | Ok(Typed ta) -> Ok ta
    | Ok(CoderBatch _)
    | Ok(InvestigatorBatch _) -> Error(InvalidIntent(toolName, "tool", "subagent intents are not ToolArgs"))
