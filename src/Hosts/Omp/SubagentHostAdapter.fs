module Wanxiangshu.Hosts.Omp.SubagentHostAdapter

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.HostAdapter
open Wanxiangshu.Runtime.HostAdapter
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Hosts.Omp.ChildSession
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.ExecutorTools
open Wanxiangshu.Hosts.Omp.OmpToolSchema
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.SubagentDispatcher
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Kernel.FallbackKernel.Types

module Dyn = Wanxiangshu.Runtime.Dyn

let private coderChildTools =
    [| "read"
       "edit"
       "write"
       "find"
       "fuzzy_find"
       "fuzzy_grep"
       "fuzzy_continue"
       "lsp"
       "inspector" |]

let private inspectorChildTools =
    [| "read"; "find"; "fuzzy_find"; "fuzzy_grep"; "fuzzy_continue" |]

type OmpHostAdapter
    (
        scope: RuntimeScope,
        pi: obj,
        ctx: obj,
        signal: obj option,
        fallbackRuntime: FallbackRuntimeStore,
        fallbackConfigOpt: FallbackConfig option
    ) =
    interface IHostAdapter with
        member _.WorkspaceRoot = Dyn.str ctx "cwd"

        member _.SessionId =
            let s = Dyn.get ctx "sessionId"
            if Dyn.isNullish s then "" else string s

        member _.SpawnSubagent(request: SubagentRequest) =
            let toolNames =
                match request.Role with
                | Coder -> coderChildTools
                | Inspector -> inspectorChildTools
                | Meditator -> [||]
                | Browser -> [| "browser" |]

            promise {
                try
                    let! (text, childID) =
                        runSubagentWithId scope pi ctx toolNames request.Prompt signal fallbackRuntime fallbackConfigOpt

                    return Success text
                with ex ->
                    return Failure(translateJsError ex)
            }

        member _.ContinueSubagent(childID: string, agent: string, prompt: string) =
            let _toolNames = [||]

            promise {
                try
                    let! text =
                        Wanxiangshu.Hosts.Omp.ChildSessionRegistry.runSubagentOnExistingSession
                            scope
                            pi
                            ctx
                            childID
                            prompt
                            signal
                            fallbackRuntime
                            fallbackConfigOpt

                    return Success text
                with ex ->
                    return Failure(translateJsError ex)
            }

        member _.RegisterTempFiles(prompt, files) =
            let sessionId =
                let s = Dyn.get ctx "sessionId"
                if Dyn.isNullish s then "" else string s

            let key = sessionId + "\u0000" + prompt
            scope.RegisterTempFiles(key, files)

        member _.TryGetTempFiles(prompt) =
            let sessionId =
                let s = Dyn.get ctx "sessionId"
                if Dyn.isNullish s then "" else string s

            let key = sessionId + "\u0000" + prompt
            scope.TryGetTempFiles(key)
