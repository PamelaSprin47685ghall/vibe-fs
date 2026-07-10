module Wanxiangshu.Omp.SubagentTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.HostAdapter
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Omp.ChildSession
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Omp.ExecutorTools
open Wanxiangshu.Omp.OmpToolSchema
open Wanxiangshu.Shell.ErrorClassify
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.SubagentDispatcher
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Kernel.FallbackKernel.Types

module Dyn = Wanxiangshu.Shell.Dyn

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
        fallbackRuntime: FallbackRuntimeState,
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
                        Wanxiangshu.Omp.ChildSessionRegistry.runSubagentOnExistingSession
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

let registerSubagentTools
    (pi: obj)
    (fallbackRuntime: FallbackRuntimeState)
    (fallbackConfigOpt: FallbackConfig option)
    : unit =
    let tb = Dyn.get pi "typebox"

    pi?registerTool (
        createObj
            [ "name", box "coder"
              "label", box "Coder"
              "description", box (description "coder")
              "parameters", coderParameters tb
              "execute",
              box (fun (_id: string) (params': obj) (signal: obj) (_u: obj) (ctx: obj) ->
                  promise {
                      try
                          let adapter =
                              OmpHostAdapter(ompScope, pi, ctx, Some signal, fallbackRuntime, fallbackConfigOpt)

                          let! text = dispatch omp adapter "coder" params' ompScope None
                          return textResult text
                      with ex ->
                          return asErrorResult ex
                  }) ]
    )

    pi?registerTool (
        createObj
            [ "name", box "investigator"
              "label", box "Investigator"
              "description", box (description "investigator")
              "parameters", investigatorParameters tb
              "execute",
              box (fun (_id: string) (params': obj) (signal: obj) (_u: obj) (ctx: obj) ->
                  promise {
                      try
                          let adapter =
                              OmpHostAdapter(ompScope, pi, ctx, Some signal, fallbackRuntime, fallbackConfigOpt)

                          let! text = dispatch omp adapter "investigator" params' ompScope None
                          return textResult text
                      with ex ->
                          return asErrorResult ex
                  }) ]
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
                          let hasBrowser =
                              not (Dyn.isNullish (Dyn.get pi "getAllTools"))
                              && (unbox<obj array> (Dyn.callMethod0 pi "getAllTools")
                                  |> Array.exists (fun t -> string t = "browser"))

                          if not hasBrowser then
                              return errorResult "Built-in browser tool is unavailable in this session."
                          else
                              let adapter =
                                  OmpHostAdapter(ompScope, pi, ctx, Some signal, fallbackRuntime, fallbackConfigOpt)

                              let! text = dispatch omp adapter "browser" params' ompScope None
                              return textResult text
                      with ex ->
                          return asErrorResult ex
                  }) ]
    )

    pi?registerTool (
        createObj
            [ "name", box "continue"
              "label", box "Continue"
              "description", box (description "continue")
              "parameters", continueParameters tb
              "execute",
              box (fun (_id: string) (params': obj) (signal: obj) (_u: obj) (ctx: obj) ->
                  promise {
                      try
                          let adapter =
                              OmpHostAdapter(ompScope, pi, ctx, Some signal, fallbackRuntime, fallbackConfigOpt)

                          let! text = dispatch omp adapter "continue" params' ompScope None
                          return textResult text
                      with ex ->
                          return asErrorResult ex
                  }) ]
    )
