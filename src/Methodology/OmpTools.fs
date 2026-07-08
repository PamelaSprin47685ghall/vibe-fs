module Wanxiangshu.Methodology.OmpTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Methodology.SchemaCommon
open Wanxiangshu.Methodology.Args
open Wanxiangshu.Methodology.Registry
open Wanxiangshu.Kernel.Subagent
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Omp
open Wanxiangshu.Omp.ChildSession
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Omp.ExecutorTools
open Wanxiangshu.Omp.OmpToolSchema
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Kernel.FallbackKernel.Types

module Dyn = Wanxiangshu.Shell.Dyn

let private executeMethodology
    (pi: obj)
    (fallbackRuntime: FallbackRuntimeState)
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
                        let intent = renderMeditatorIntent entry parsed.intent parsed.note
                        let prompt = formatPrompt omp (Meditator(intent, [])) |> List.head

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

let registerMethodologyTools
    (pi: obj)
    (fallbackRuntime: FallbackRuntimeState)
    (fallbackConfigOpt: FallbackConfig option)
    : unit =
    let tb = Dyn.get pi "typebox"

    pi?registerTool (
        createObj
            [ "name", box unifiedToolName
              "label", box unifiedToolName
              "description", box unifiedToolDescription
              "parameters", box (methodologyParameters tb)
              "execute", box (executeMethodology pi fallbackRuntime fallbackConfigOpt) ]
    )
