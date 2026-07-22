module Wanxiangshu.Hosts.Omp.OmpTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Methodology.Schema
open Wanxiangshu.Runtime.MethodologyArgs
open Wanxiangshu.Kernel.Methodology.Registry
open Wanxiangshu.Runtime.Subagent
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Hosts.Omp
open Wanxiangshu.Hosts.Omp.ChildSession
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.ExecutorTools
open Wanxiangshu.Hosts.Omp.OmpToolSchema
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Kernel.FallbackKernel.Types

module Dyn = Wanxiangshu.Runtime.Dyn

let private executeMethodology
    (pi: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    (fallbackConfigOpt: FallbackConfig option)
    =
    System.Func<string, obj, obj, obj, obj, JS.Promise<ToolResult>>
        (fun (_id: string) (params': obj) (signal: obj) (_u: obj) (ctx: obj) ->
            promise {
                match parse params' with
                | Error message -> return errorResult ("Error: " + message)
                | Ok parsed ->
                    match tryFindEntry parsed.methodology with
                    | None -> return errorResult ("Error: unknown methodology: " + parsed.methodology)
                    | Some entry ->
                        let intent = Wanxiangshu.Runtime.SubagentPrompts.renderMeditatorIntent entry parsed.intent parsed.background parsed.note
                        let prompt = formatPrompt omp (Meditator intent) |> List.head

                        try
                            let! text =
                                runSubagent
                                    ExecutorTools.ompScope
                                    pi
                                    ctx
                                    [||]
                                    prompt
                                    (Some signal)
                                    fallbackRuntime
                                    fallbackConfigOpt

                            return textResult text
                        with ex ->
                            return asErrorResult ex
            })

let registerMeditatorTools
    (pi: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    (fallbackConfigOpt: FallbackConfig option)
    : unit =
    let tb = Dyn.get pi "typebox"

    pi?registerTool (
        createObj
            [ "name", box unifiedToolName
              "label", box unifiedToolName
              "description", box unifiedToolDescription
              "parameters", box (meditatorParameters tb)
              "execute", box (executeMethodology pi fallbackRuntime fallbackConfigOpt) ]
    )
