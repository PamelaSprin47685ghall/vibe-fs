module Wanxiangshu.Omp.ExecutorTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Kernel.SubagentPrompts
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Omp.ChildSession
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Omp.MessagingCodec
open Wanxiangshu.Omp.OmpToolSchema
open Wanxiangshu.Omp.Schema

module Dyn = Wanxiangshu.Shell.Dyn

let private description (name: string) : string =
    match Wanxiangshu.Kernel.ToolCatalog.description name with
    | Ok d -> d
    | Error e -> failwith e

open Wanxiangshu.Shell.Executor
open Wanxiangshu.Shell.RunnerBackground
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.SessionExecutor
open Wanxiangshu.Shell.SubagentIo

[<Global("Buffer")>]
let private nodeBuffer: obj = jsNative

let private byteLength (s: string) : int = nodeBuffer?byteLength (s, "utf-8")

let ompScope = RuntimeScope()
let private sessionExecutor = createForScope ompScope

let private parseExecutorParams (params': obj) (ctx: obj) =
    let lang = let l = Dyn.str params' "language" in if l = "" then "shell" else l
    let program = Dyn.str params' "program"
    let what = Dyn.str params' "what_to_summarize"
    let timeoutType = parseTimeout (Dyn.str params' "timeout_type")
    let mode = let m = Dyn.str params' "mode" in if m = "" then "rw" else m

    let deps =
        let d = Dyn.get params' "dependencies"

        if Dyn.isNullish d || not (Dyn.isArray d) then
            []
        else
            unbox<obj array> d |> Array.map string |> List.ofArray

    let cwd = Dyn.str ctx "cwd"
    (lang, program, what, timeoutType, mode, deps, cwd)

let private runExecutorJob (options: ExecuteOptions) (signal: obj) (childId: string) =
    promise {
        let runWork =
            sessionExecutor.EnqueuePerSession(
                childId,
                fun () ->
                    promise {
                        let! r = executeWith defaultExecuteDeps options childId None
                        return r
                    }
            )

        let onSignalAbort () =
            if childId <> "" then
                abortExecutorRun childId

        return! raceWithAbortSignal signal onSignalAbort runWork
    }

let private summarizeOutput
    (pi: obj)
    (childSession: obj)
    (output: string)
    (lang: string)
    (program: string)
    (deps: string list)
    (mode: string)
    (what: string)
    =
    promise {
        let summaryPrompt =
            executorSummarizerPrompt what output lang program deps "executor" mode

        do! childSession?prompt (summaryPrompt) |> unbox<JS.Promise<unit>>
        do! childSession?waitForIdle () |> unbox<JS.Promise<unit>>
        let sm = unbox<ISessionManager> (Dyn.get childSession "sessionManager")
        let text = readAssistantText sm 0 "\n\n" |> Option.defaultValue noOutputText
        return textResult text
    }

let private executeExecutor (pi: obj) (_id: string) (params': obj) (signal: obj) (_onUpdate: obj) (ctx: obj) =
    promise {
        let (lang, program, what, timeoutType, mode, deps, cwd) =
            parseExecutorParams params' ctx

        let mutable childHolder: ChildSession option = None

        let disposeChild () =
            match childHolder with
            | None -> ()
            | Some child ->
                try
                    if not (Dyn.isNullish (Dyn.get child.session "abort")) then
                        try
                            Dyn.callMethod0 child.session "abort" |> ignore
                        with _ ->
                            ()

                    child.dispose |> Option.iter (fun dispose -> dispose ())
                with _ ->
                    ()

                childHolder <- None

        let finishJob () = disposeChild ()

        if System.String.IsNullOrWhiteSpace what then
            return errorResult "Executor: what_to_summarize is required."
        else
            try
                let! child = createChildSession ompScope pi ctx [||] None [||] None
                childHolder <- Some child
                let childSession = child.session
                let childCtx = createObj [ "sessionManager", Dyn.get childSession "sessionManager" ]
                let childId = getSessionIdFromContext childCtx |> Option.defaultValue ""

                if childId = "" then
                    finishJob ()
                    return errorResult "Executor child session unavailable"
                else
                    let options =
                        { program = program
                          language = parseLanguage lang
                          dependencies = deps
                          timeoutType = timeoutType
                          mode = mode
                          cwd = Some cwd
                          whatToSummarize = what }

                    let! result = runExecutorJob options signal childId
                    let output = outputFromResult result

                    if not (shouldSummarize byteLength output) then
                        finishJob ()
                        return textResult output
                    else
                        let! text = summarizeOutput pi childSession output lang program deps mode what
                        finishJob ()
                        return text
            with ex ->
                finishJob ()

                if hasErrorName ex "AbortError" then
                    return textResult "Executor aborted."
                else
                    return asErrorResult ex
    }

let registerExecutorTools (pi: obj) : unit =
    let tb = Dyn.get pi "typebox"

    pi?registerTool (
        createObj
            [ "name", box "executor"
              "label", box "Executor"
              "description", box (description "executor")
              "parameters", executorParameters tb
              "execute", box (fun id p s u c -> executeExecutor pi id p s u c) ]
    )
