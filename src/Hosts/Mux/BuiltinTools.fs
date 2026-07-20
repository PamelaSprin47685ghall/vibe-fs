module Wanxiangshu.Hosts.Mux.BuiltinTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Runtime.ExecutorFormat
open Wanxiangshu.Kernel.FuzzyPath
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Kernel.FuzzyFormat
open Wanxiangshu.Runtime.Subagent
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.ToolCopy
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Hosts.Mux.Delegate
open Wanxiangshu.Hosts.Mux.Wrappers
open Wanxiangshu.Hosts.Mux.WrappersReview
open Wanxiangshu.Hosts.Mux.SubagentTools
open Wanxiangshu.Runtime.FileSys
open Wanxiangshu.Runtime.FuzzyFinderShell
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.ExecutorToolsCodec
open Wanxiangshu.Runtime.FileToolsCodec
open Wanxiangshu.Hosts.Mux.BuiltinToolsFuzzy
open Wanxiangshu.Runtime.ToolExecute
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Hosts.Mux.BuiltinToolsHelpers

let private description (name: string) : string =
    match Wanxiangshu.Kernel.ToolCatalog.description name with
    | Ok d -> d
    | Error e -> failwith e

let summarizationAgentId = BuiltinToolsHelpers.summarizationAgentId
let summarizationRole = BuiltinToolsHelpers.summarizationRole
let summarizationAiSettingsAgentId = BuiltinToolsHelpers.summarizationAiSettingsAgentId

let addIfSome (entries: ResizeArray<string * obj>) (key: string) (v: 'T option) =
    match v with
    | Some x -> entries.Add(key, box x)
    | None -> ()

let executorTool
    (deps: obj)
    (toolNames: string array)
    (sessionScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    : ToolDefinition =
    { name = "executor"
      description = description "executor"
      parameters =
        mkSchema
            (createObj
                [ "language",
                  box (strEnumPropWithDefault Params.executorLanguage [| "shell"; "python"; "javascript" |] "shell")
                  "command", box (strProp Params.executorCommand)
                  "dependencies", box (strArrayProp Params.executorDeps)
                  "timeout_type", box (strEnumProp Params.executorTimeout [| "short"; "long" |])
                  "mode", box (strEnumProp Params.executorMode [| "ro"; "rw" |])
                  "what_to_summarize", box (strProp Params.executorWhatToSummarize) ])
            [| "command"; "timeout_type"; "mode"; "what_to_summarize" |]
      execute =
        fun config args ->
            match fromMuxConfig config with
            | Error e -> Promise.lift (wireEncodeToolError "MuxConfig" e)
            | Ok runtime ->
                let sessionId = Id.sessionIdValue runtime.Execution.SessionId

                if sessionId = "" then
                    Promise.lift executorRequiresSession
                else
                    match decodeExecutorArgs args with
                    | Error e -> Promise.lift (wireDomainFailure "Executor" e)
                    | Ok decoded ->
                        promise {
                            let opts = toExecuteOptions (Some runtime.Execution.Directory) decoded

                            let! execResult =
                                sessionScope.EnqueueExecutor(
                                    sessionId,
                                    opts.mode,
                                    fun () -> Wanxiangshu.Runtime.Executor.execute sessionScope opts sessionId
                                )

                            return! summarizeWhenNeeded deps config toolNames opts execResult
                        }
      condition = None }

let readTool (_deps: obj) (hostReadExec: HostFunctionCapture) : ToolDefinition =
    { name = "read"
      description = description "read"
      parameters =
        mkSchema
            (createObj
                [ "path", box (strProp Params.readPath)
                  "offset", box (numProp Params.readOffset)
                  "limit", box (numProp Params.readLimit) ])
            [| "path" |]
      execute =
        fun config args ->
            match fromMuxConfig config with
            | Error e -> Promise.lift (wireEncodeToolError "MuxConfig" e)
            | Ok runtime ->
                let cwd = Some runtime.Execution.Directory

                promise {
                    match decodeReadArgs args with
                    | Error e -> return wireDecodeFailure "read" e
                    | Ok decoded ->
                        let path = decoded.Path
                        let offset = decoded.Offset
                        let limit = decoded.Limit

                        match hostReadExec.TryGet() with
                        | Some hostExec ->
                            let raw = Dyn.call2 hostExec (readArgsForHost decoded) config

                            let! result =
                                if Dyn.typeIs (Dyn.get raw "then") "function" then
                                    unbox<JS.Promise<obj>> raw
                                else
                                    Promise.lift raw

                            if BuiltinToolsHelpers.hostReadResultIsDirectoryError result then
                                return! read cwd path offset limit
                            else
                                return BuiltinToolsHelpers.formatHostReadResult result
                        | None -> return! read cwd path offset limit
                }
      condition = None }

let writeTool (_deps: obj) : ToolDefinition =
    { name = "write"
      description = description "write"
      parameters =
        mkSchema
            (createObj
                [ "file_path", box (strProp Params.writeFilePath)
                  "content", box (strProp Params.writeContent) ])
            [| "file_path"; "content" |]
      execute =
        fun config args ->
            match fromMuxConfig config with
            | Error e -> Promise.lift (wireEncodeToolError "MuxConfig" e)
            | Ok runtime ->
                let cwd = Some runtime.Execution.Directory

                promise {
                    match decodeWriteArgs args with
                    | Error e -> return wireDecodeFailure "write" e
                    | Ok decoded ->
                        let! result = write cwd decoded.FilePath decoded.Content

                        match result with
                        | Ok msg -> return msg
                        | Error e -> return wireDomainFailure "write" e
                }
      condition = None }

let fuzzyFindTool = BuiltinToolsFuzzy.fuzzyFindTool
let fuzzyGrepTool = BuiltinToolsFuzzy.fuzzyGrepTool
let fuzzyContinueTool = BuiltinToolsFuzzy.fuzzyContinueTool
