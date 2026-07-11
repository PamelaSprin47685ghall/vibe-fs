module Wanxiangshu.Shell.DelegateToolsCodec

open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.DynField
open Wanxiangshu.Shell.ToolContextCodec

type DelegateHostConfig =
    { WorkspaceId: WorkspaceId
      TaskService: obj
      AbortSignal: obj }

type DelegateOptionsFields =
    { Experiments: obj
      AiSettingsAgentId: string }

let decodeSubagentRole (config: obj) : string =
    defaultArg (strField config "subagentRole") ""

let decodeDelegateConfig (config: obj) : Result<DelegateHostConfig, DomainError> =
    match decodeMuxConfig (unbox<IMuxToolContext> config) with
    | Error e -> Error e
    | Ok ctx ->
        match ctx.WorkspaceId with
        | None -> Error(InvalidIntent("delegate", "workspaceId", "required"))
        | Some wid ->
            let taskService = Dyn.get config "taskService"

            if isNull taskService then
                Error(InvalidIntent("delegate", "taskService", "missing"))
            else
                Ok
                    { WorkspaceId = wid
                      TaskService = taskService
                      AbortSignal = Dyn.get config "abortSignal" }

let decodeDelegateOptions (options: obj) : DelegateOptionsFields =
    { Experiments = Dyn.get options "experiments"
      AiSettingsAgentId = defaultArg (strField options "aiSettingsAgentId") "" }

let decodeTaskCreateResult (createResult: obj) : Result<string, DomainError> =
    if Dyn.isNullish createResult then
        Error(InvalidIntent("delegate", "createResult", "missing"))
    else
        let success = Dyn.truthy (Dyn.get createResult "success")
        let err = defaultArg (strField createResult "error") ""

        if not success then
            let msg = if err <> "" then err else "create failed"
            Error(InvalidIntent("delegate.create", "taskService", msg))
        else
            let data = Dyn.get createResult "data"

            let taskId =
                if Dyn.isNullish data then
                    ""
                else
                    defaultArg (strField data "taskId") ""

            if taskId.Trim() = "" then
                Error(InvalidIntent("delegate.create", "taskId", "missing or empty"))
            else
                Ok taskId

let decodeTaskReport (report: obj) : Result<string, DomainError> =
    if Dyn.isNullish report then
        Error(InvalidIntent("delegate", "report", "missing"))
    else
        let markdown = defaultArg (strField report "reportMarkdown") ""

        if markdown.Trim() = "" then
            Error(InvalidIntent("delegate.report", "reportMarkdown", "missing or empty"))
        else
            Ok markdown
