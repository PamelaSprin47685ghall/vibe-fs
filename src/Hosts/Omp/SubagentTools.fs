module Wanxiangshu.Hosts.Omp.SubagentTools

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

let private description (name: string) : string =
    match Wanxiangshu.Kernel.ToolCatalog.description name with
    | Ok d -> d
    | Error e -> failwith e

let private coderChildTools =
    [| "read"
       "edit"
       "write"
       "find"
       "fuzzy_find"
       "fuzzy_grep"
       "fuzzy_continue"
       "lsp"
       "investigator" |]

let private investigatorChildTools =
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
                | Investigator -> investigatorChildTools
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

let private dispatchSubagent
    (toolName: string)
    (params': obj)
    (pi: obj)
    (ctx: obj)
    (signal: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    (fallbackConfigOpt: FallbackConfig option)
    =
    promise {
        try
            let adapter =
                OmpHostAdapter(ompScope, pi, ctx, Some signal, fallbackRuntime, fallbackConfigOpt)

            let! text = dispatch omp adapter toolName params' ompScope None
            return textResult text
        with ex ->
            return asErrorResult ex
    }

let private buildSubagentInvocation
    (toolName: string)
    (label: string)
    (desc: string)
    (paramsBuilder: obj -> obj)
    (pi: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    (fallbackConfigOpt: FallbackConfig option)
    =
    let tb = Dyn.get pi "typebox"

    let execute =
        box (fun (_id: string) (params': obj) (signal: obj) (_u: obj) (ctx: obj) ->
            dispatchSubagent toolName params' pi ctx signal fallbackRuntime fallbackConfigOpt)

    createObj
        [ "name", box toolName
          "label", box label
          "description", box desc
          "parameters", paramsBuilder tb
          "execute", execute ]

let private hasBuiltinBrowser (pi: obj) =
    not (Dyn.isNullish (Dyn.get pi "getAllTools"))
    && (unbox<obj array> (Dyn.callMethod0 pi "getAllTools")
        |> Array.exists (fun t -> string t = "browser"))

let registerSubagentTools
    (pi: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    (fallbackConfigOpt: FallbackConfig option)
    : unit =
    let tb = Dyn.get pi "typebox"

    pi?registerTool (
        buildSubagentInvocation
            "coder"
            "Coder"
            (description "coder")
            coderParameters
            pi
            fallbackRuntime
            fallbackConfigOpt
    )

    pi?registerTool (
        buildSubagentInvocation
            "investigator"
            "Investigator"
            (description "investigator")
            investigatorParameters
            pi
            fallbackRuntime
            fallbackConfigOpt
    )

    pi?registerTool (
        createObj
            [ "name", box "browser"
              "label", box "Browser"
              "description", box (description "browser")
              "parameters", browserParameters tb
              "execute",
              box (fun (_id: string) (params': obj) (signal: obj) (_u: obj) (ctx: obj) ->
                  promise {
                      try
                          if not (hasBuiltinBrowser pi) then
                              return errorResult "Built-in browser tool is unavailable in this session."
                          else
                              return!
                                  dispatchSubagent "browser" params' pi ctx signal fallbackRuntime fallbackConfigOpt
                      with ex ->
                          return asErrorResult ex
                  }) ]
    )

    pi?registerTool (
        buildSubagentInvocation
            "continue"
            "Continue"
            (description "continue")
            continueParameters
            pi
            fallbackRuntime
            fallbackConfigOpt
    )
