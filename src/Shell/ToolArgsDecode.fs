module VibeFs.Shell.ToolArgsDecode

open VibeFs.Kernel.Domain
open VibeFs.Kernel.ToolArgs
open VibeFs.Kernel.SubagentIntents
open VibeFs.Shell.Dyn
open VibeFs.Shell.SubagentIntentsCodec
open VibeFs.Shell.SubagentSimpleArgsCodec
open VibeFs.Shell.WebToolsCodec
open VibeFs.Shell.ExecutorToolsCodec
open VibeFs.Shell.WorkBacklogToolsCodec
open VibeFs.Shell.KnowledgeGraphToolsCodec
open VibeFs.Shell.PatchToolsCodec
open VibeFs.Shell.ReviewToolsCodec

let private mapRead (r: FileToolsCodec.ReadArgs) : VibeFs.Kernel.ToolArgs.ReadArgs =
    { Path = r.Path; Offset = r.Offset; Limit = r.Limit }

let private mapWrite (w: FileToolsCodec.WriteArgs) : VibeFs.Kernel.ToolArgs.WriteArgs =
    { FilePath = w.FilePath; Content = w.Content }

let private mapWebsearch (w: WebToolsCodec.WebsearchArgs) : VibeFs.Kernel.ToolArgs.WebsearchArgs =
    { Query = w.Query; NumResults = w.NumResults; WhatToSummarize = w.WhatToSummarize }

let private mapWebfetch (w: WebToolsCodec.WebfetchArgs) : VibeFs.Kernel.ToolArgs.WebfetchArgs =
    {
        Url = w.Url
        ExtractMain = w.ExtractMain
        PreferLlmsTxt = w.PreferLlmsTxt
        Prompt = w.Prompt
        Timeout = w.Timeout
    }

let private mapExecutor (e: ExecutorToolsCodec.ExecutorArgs) : VibeFs.Kernel.ToolArgs.ExecutorArgs =
    {
        Language = e.Language
        Program = e.Program
        Dependencies = e.Dependencies
        TimeoutType = e.TimeoutType
        Mode = e.Mode
    }

let private mapTodoItem (t: WorkBacklogToolsCodec.TodoItem) : VibeFs.Kernel.ToolArgs.TodoItem =
    { Content = t.Content; Status = t.Status; Priority = t.Priority }

let private mapTodoWrite (tw: WorkBacklogToolsCodec.TodoWriteArgs) : VibeFs.Kernel.ToolArgs.TodoWriteArgs =
    {
        CompletedWorkReport = tw.CompletedWorkReport
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
    | "knowledge_graph_fetch" ->
        decodeFetchEntity args
        |> Result.map (fun entity -> Typed (ToolArgs.KnowledgeGraphFetch { Entity = entity }))
    | "return_bookkeeper" ->
        decodeReturnBookkeeperArgs args
        |> Result.map (fun drafts -> Typed (ToolArgs.ReturnBookkeeper drafts))
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