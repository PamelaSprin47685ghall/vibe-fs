module Wanxiangshu.Hosts.Omp.ExecutorTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Runtime.ExecutorFormat
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Runtime.SubagentPrompts
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Hosts.Omp.ChildSession
open Wanxiangshu.Hosts.Omp.ChildCleanup
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.MessagingCodec
open Wanxiangshu.Hosts.Omp.OmpToolSchema
open Wanxiangshu.Hosts.Omp.Schema
open Wanxiangshu.Runtime.DynField

module Dyn = Wanxiangshu.Runtime.Dyn

let private description (name: string) : string =
    match Wanxiangshu.Kernel.ToolCatalog.description name with
    | Ok d -> d
    | Error e -> failwith e

open Wanxiangshu.Runtime.Executor
open Wanxiangshu.Runtime.RunnerBackground
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.SessionExecutor
open Wanxiangshu.Runtime.SubagentIo
open Wanxiangshu.Runtime.OmpHostBindings

[<Global("Buffer")>]
let private nodeBuffer: obj = jsNative

let private byteLength (s: string) : int = nodeBuffer?byteLength (s, "utf-8")

let ompScope = RuntimeScope()
let private sessionExecutor = createForScope ompScope

let private parseExecutorParams (params': obj) (ctx: obj) =
    let lang = let l = Dyn.str params' "language" in if l = "" then "shell" else l
    let command = Dyn.str params' "command"
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

    let maxBytes =
        match optInt params' "max_bytes" with
        | Some mb -> mb
        | None -> 8192

    (lang, command, what, timeoutType, mode, deps, cwd, maxBytes)

let private runExecutorJob (options: ExecuteOptions) (signal: obj) (childId: string) =
    promise {
        let runWork =
            sessionExecutor.EnqueueExecutor(
                childId,
                options.mode,
                fun () ->
                    promise {
                        let! r = executeWith defaultExecuteDeps ompScope options childId None
                        return r
                    }
            )

        let onSignalAbort () =
            if childId <> "" then
                abortExecutorRun ompScope childId

        return! raceWithAbortSignal signal onSignalAbort runWork
    }

let private summarizeOutput
    (_pi: obj)
    (childSession: obj)
    (output: string)
    (lang: string)
    (command: string)
    (deps: string list)
    (mode: string)
    (what: string)
    =
    promise {
        let summaryPrompt =
            executorSummarizerPrompt what output lang command deps "executor" mode

        // Anchor to target turn: baseline before prompt; wait idle only if transcript grew.
        // Bare waitForIdle alone can observe a pre-existing idle (SPEC §4.5 #8).
        let baseline = entryCountOfSession childSession
        let! _ = sessionPrompt childSession summaryPrompt
        let! grew = waitForIdleAfterBaseline childSession baseline 8
        let sm = unbox<ISessionManager> (Dyn.get childSession "sessionManager")

        let text =
            if grew then
                match readAssistantText sm baseline "\n\n" with
                | Some t -> t
                | None -> output
            else
                output

        return textResult text
    }

let private buildExecutorRequest (params': obj) (ctx: obj) =
    let (lang, command, what, timeoutType, mode, deps, cwd, maxBytes) =
        parseExecutorParams params' ctx

    let options =
        { command = command
          language = parseLanguage lang
          dependencies = deps
          timeoutType = timeoutType
          mode = mode
          cwd = Some cwd
          whatToSummarize = what
          maxBytes = maxBytes }

    (lang, what, options)

let private parseExecutorResponse
    (pi: obj)
    (childSession: obj)
    (childHolder: ChildSession option)
    (result: ExecuteResult)
    (options: ExecuteOptions)
    (lang: string)
    (what: string)
    =
    promise {
        let output = outputFromResult result

        if not (shouldSummarize byteLength options.maxBytes output) then
            return textResult output
        else
            let! text =
                summarizeOutput pi childSession output lang options.command options.dependencies options.mode what

            return text
    }

let private executeExecutor (pi: obj) (_id: string) (params': obj) (signal: obj) (_onUpdate: obj) (ctx: obj) =
    promise {
        let (lang, what, options) = buildExecutorRequest params' ctx

        let mutable childHolder: ChildSession option = None

        let disposeChild () =
            match childHolder with
            | None -> ()
            | Some child ->
                CleanupChildSession child.session child.dispose
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
                    let! result = runExecutorJob options signal childId
                    let! response = parseExecutorResponse pi childSession childHolder result options lang what
                    finishJob ()
                    return response
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
