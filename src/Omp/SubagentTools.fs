module Wanxiangshu.Omp.SubagentTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.HostAdapter
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Omp.ChildSession
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Omp.OmpToolSchema
open Wanxiangshu.Shell.ErrorClassify
open Wanxiangshu.Shell.SubagentDispatcher
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Kernel.FallbackKernel.Types
module Dyn = Wanxiangshu.Shell.Dyn

let private coderChildTools = [| "read"; "edit"; "write"; "find"; "fuzzy_find"; "fuzzy_grep"; "lsp"; "investigator" |]
let private investigatorChildTools = [| "read"; "find"; "fuzzy_find"; "fuzzy_grep" |]

type OmpHostAdapter(pi: obj, ctx: obj, signal: obj option, fallbackRuntime: FallbackRuntimeState, fallbackConfigOpt: FallbackConfig option) =
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
                    let! text = runSubagent pi ctx toolNames request.Prompt signal fallbackRuntime fallbackConfigOpt
                    return Success text
                with ex ->
                    return Failure (translateJsError ex)
            }

let registerSubagentTools (pi: obj) (fallbackRuntime: FallbackRuntimeState) (fallbackConfigOpt: FallbackConfig option) : unit =
    let tb = Dyn.get pi "typebox"

    pi?registerTool(
        createObj [
            "name", box "coder"
            "label", box "Coder"
            "description", box (description "coder")
            "parameters", coderParameters tb
            "execute",
                box(fun (_id: string) (params': obj) (signal: obj) (_u: obj) (ctx: obj) ->
                    promise {
                        try
                            let adapter = OmpHostAdapter(pi, ctx, Some signal, fallbackRuntime, fallbackConfigOpt)
                            let! text = dispatch omp adapter "coder" params'
                            return textResult text
                        with ex -> return asErrorResult ex
                    })
        ])

    pi?registerTool(
        createObj [
            "name", box "investigator"
            "label", box "Investigator"
            "description", box (description "investigator")
            "parameters", investigatorParameters tb
            "execute",
                box(fun (_id: string) (params': obj) (signal: obj) (_u: obj) (ctx: obj) ->
                    promise {
                        try
                            let adapter = OmpHostAdapter(pi, ctx, Some signal, fallbackRuntime, fallbackConfigOpt)
                            let! text = dispatch omp adapter "investigator" params'
                            return textResult text
                        with ex -> return asErrorResult ex
                    })
        ])

    pi?registerTool(
        createObj [
            "name", box "meditator"
            "label", box "Meditator"
            "description", box (description "meditator")
            "parameters", meditatorParameters tb
            "execute",
                box(fun (_id: string) (params': obj) (signal: obj) (_u: obj) (ctx: obj) ->
                    promise {
                        try
                            let adapter = OmpHostAdapter(pi, ctx, Some signal, fallbackRuntime, fallbackConfigOpt)
                            let! text = dispatch omp adapter "meditator" params'
                            return textResult text
                        with ex -> return asErrorResult ex
                    })
        ])

    pi?registerTool(
        createObj [
            "name", box "browser"
            "label", box "Browser"
            "description", box (description "browser")
            "parameters", browserParameters tb
            "execute",
                box(fun (_id: string) (params': obj) (signal: obj) (_u: obj) (ctx: obj) ->
                    promise {
                        try
                            let getAll = Dyn.get pi "getAllTools"
                            let hasBrowser =
                                Dyn.typeIs getAll "function"
                                && (unbox<obj array> (Dyn.call0 getAll) |> Array.exists (fun t -> string t = "browser"))
                            if not hasBrowser then
                                return errorResult "Built-in browser tool is unavailable in this session."
                            else
                                let adapter = OmpHostAdapter(pi, ctx, Some signal, fallbackRuntime, fallbackConfigOpt)
                                let! text = dispatch omp adapter "browser" params'
                                return textResult text
                        with ex -> return asErrorResult ex
                    })
        ])
