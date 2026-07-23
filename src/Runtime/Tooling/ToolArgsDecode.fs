module Wanxiangshu.Runtime.ToolArgsDecode

open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.SubagentIntentsCodec
open Wanxiangshu.Runtime.SubagentSimpleArgsCodec
open Wanxiangshu.Runtime.ExecutorToolsCodec
open Wanxiangshu.Runtime.PatchToolsCodec
open Wanxiangshu.Runtime.ReviewToolsCodec

let private mapRead (r: FileToolsCodec.ReadArgs) : Wanxiangshu.Kernel.ToolArgs.ReadArgs =
    { Path = r.Path
      Offset = r.Offset
      Limit = r.Limit }

let private mapWrite (w: FileToolsCodec.WriteArgs) : Wanxiangshu.Kernel.ToolArgs.WriteArgs =
    { FilePath = w.FilePath
      Content = w.Content }



let private mapExecutor (e: ExecutorToolsCodec.ExecutorArgs) : Wanxiangshu.Kernel.ToolArgs.ExecutorArgs =
    { Language = e.Language
      Command = e.Command
      Dependencies = e.Dependencies
      TimeoutType = e.TimeoutType
      WhatToSummarize = e.WhatToSummarize }

type DecodedToolInvocation =
    | Typed of ToolArgs
    | CoderBatch of CoderIntent list
    | InspectorBatch of InspectorIntent list

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

let private decodeInspector args =
    intentsField "inspector" args
    |> Result.bind (fun raw ->
        parseInspectorIntents raw
        |> Result.mapError (fun msg -> ParseError("intents", msg))
        |> Result.map InspectorBatch)

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



let private decodeExecutor args =
    ExecutorToolsCodec.decodeExecutorArgs args
    |> Result.map (fun e -> Typed(ToolArgs.Executor(mapExecutor e)))

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
    | "inspector" -> decodeInspector args
    | "browser" -> decodeBrowser args
    | "continue" -> decodeContinue args
    | "executor" -> decodeExecutor args
    | "apply_patch" -> decodeApplyPatch args
    | "submit_review" -> decodeSubmitReview args
    | _ -> Error(InvalidIntent(toolName, "tool", "unknown tool for ToolArgs decode"))

let decodeToolArgs (toolName: string) (args: obj) : Result<ToolArgs, DomainError> =
    match decodeToolInvocation toolName args with
    | Error e -> Error e
    | Ok(Typed ta) -> Ok ta
    | Ok(CoderBatch _)
    | Ok(InspectorBatch _) -> Error(InvalidIntent(toolName, "tool", "subagent intents are not ToolArgs"))
