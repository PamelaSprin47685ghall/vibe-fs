module VibeFs.Omp.ExecutorTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Executor
open VibeFs.Kernel.OmpSessionTools
open VibeFs.Kernel.SubagentPrompts
open VibeFs.Kernel.ToolCatalog
open VibeFs.Omp.ChildSession
open VibeFs.Omp.Codec
open VibeFs.Omp.MessagingCodec
open VibeFs.Omp.OmpToolSchema
open VibeFs.Omp.Schema
module Dyn = VibeFs.Shell.Dyn
open VibeFs.Shell.Executor
open VibeFs.Shell.RunnerBackground
open VibeFs.Shell.SessionExecutor
open VibeFs.Shell.SubagentIo

[<Global("Buffer")>]
let private nodeBuffer : obj = jsNative
let private byteLength (s: string) : int = nodeBuffer?byteLength(s, "utf-8")

let executorMaxWaitMs = 60_000
let executorMinWaitMs = 500

let registerExecutorTools (pi: obj) : unit =
    let tb = Dyn.get pi "typebox"

    pi?registerTool(
        createObj [
            "name", box "executor"
            "label", box "Executor"
            "description", box (description "executor")
            "parameters", executorParameters tb
            "execute",
                box(fun (_id: string) (params': obj) (signal: obj) (_onUpdate: obj) (ctx: obj) ->
                    promise {
                        let parentId = getSessionIdFromContext ctx |> Option.defaultValue ""
                        let lang =
                            let l = Dyn.str params' "language"
                            if l = "" then "shell" else l
                        let program = Dyn.str params' "program"
                        let what = Dyn.str params' "what_to_summarize"
                        let timeoutType = parseTimeout (Dyn.str params' "timeout_type")
                        let mode =
                            let m = Dyn.str params' "mode"
                            if m = "" then "rw" else m
                        let deps =
                            let d = Dyn.get params' "dependencies"
                            if Dyn.isNullish d || not (Dyn.isArray d) then []
                            else unbox<obj array> d |> Array.map string |> List.ofArray
                        let mutable childHolder : ChildSession option = None
                        let disposeChild () =
                            match childHolder with
                            | None -> ()
                            | Some child ->
                                try
                                    let abort = Dyn.get child.session "abort"
                                    if Dyn.typeIs abort "function" then Dyn.call0 abort |> ignore
                                    child.dispose |> Option.iter (fun dispose -> dispose ())
                                with _ -> ()
                                childHolder <- None
                        let finishJob () =
                            if parentId <> "" then
                                unregisterActiveRunnerSession parentId
                                unregisterRunnerChild parentId
                            disposeChild ()
                        try
                            let! child = createChildSession pi ctx ompRunnerChildToolNames None [||]
                            childHolder <- Some child
                            let childSession = child.session
                            let childCtx = createObj [ "sessionManager", Dyn.get childSession "sessionManager" ]
                            let childId = getSessionIdFromContext childCtx |> Option.defaultValue ""
                            if childId = "" then
                                finishJob ()
                                return errorResult "Executor child session unavailable"
                            else
                                if parentId <> "" then
                                    registerActiveRunnerSession parentId
                                    registerRunnerChild parentId childId disposeChild
                                let options =
                                    { program = program
                                      language = parseLanguage lang
                                      dependencies = deps
                                      timeoutType = timeoutType
                                      mode = mode
                                      cwd = Some (Dyn.str ctx "cwd") }
                                let runWork =
                                    enqueuePerSession childId (fun () ->
                                        promise {
                                            let! r = executeWith defaultExecuteDeps options childId None
                                            let output = outputFromResult r
                                            appendRunnerLog childId output
                                            if parentId <> "" then appendRunnerLog parentId output
                                            return r
                                        })
                                let onSignalAbort () =
                                    if parentId <> "" then abortRunnerJob parentId |> ignore
                                    if childId <> "" then abortExecutorRun childId
                                let! result = raceWithAbortSignal signal onSignalAbort runWork
                                let output = outputFromResult result
                                if not (shouldSummarize byteLength output) then
                                    finishJob ()
                                    return textResult output
                                else
                                    let summaryPrompt =
                                        executorSummarizerPrompt what output lang program deps "executor" mode
                                    do! childSession?prompt(summaryPrompt) |> unbox<JS.Promise<unit>>
                                    do! childSession?waitForIdle() |> unbox<JS.Promise<unit>>
                                    let sm = Dyn.get childSession "sessionManager"
                                    let text = readAssistantText sm 0 "\n\n" |> Option.defaultValue noOutputText
                                    finishJob ()
                                    return textResult text
                        with ex ->
                            finishJob ()
                            if hasErrorName ex "AbortError" then return textResult "Executor aborted."
                            else return asErrorResult ex
                    })
        ])

    pi?registerTool(
        createObj [
            "name", box "executor_wait"
            "label", box "Executor Wait"
            "description", box (description "executor_wait")
            "defaultInactive", box true
            "parameters", objectOf [| ("ms", opt "Wait time in milliseconds." tb num) |] tb
            "execute",
                box(fun (_id: string) (params': obj) (_s: obj) (_u: obj) (ctx: obj) ->
                    promise {
                        match getSessionIdFromContext ctx with
                        | None -> return errorResult "No executor session found."
                        | Some sid ->
                            let ms = defaultArg (optInt params' "ms") 2000
                            let waitMs = max executorMinWaitMs (min executorMaxWaitMs ms)
                            let! snippet = waitRunnerJob sid waitMs
                            return textResult snippet
                    })
        ])

    pi?registerTool(
        createObj [
            "name", box "executor_abort"
            "label", box "Executor Abort"
            "description", box (description "executor_abort")
            "defaultInactive", box true
            "parameters", objectOf [||] tb
            "execute",
                box(fun (_id: string) (_p: obj) (_s: obj) (_u: obj) (ctx: obj) ->
                    promise {
                        match getSessionIdFromContext ctx with
                        | None -> return errorResult "No executor session found."
                        | Some sid ->
                            let msg = abortRunnerJob sid
                            return textResult msg
                    })
        ])
