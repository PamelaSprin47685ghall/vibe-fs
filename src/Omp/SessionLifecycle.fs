module VibeFs.Omp.SessionLifecycle

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.OmpSessionTools
open VibeFs.Kernel.PromptFragments
open VibeFs.Omp.Codec
open VibeFs.Omp.MessageTransform
open VibeFs.Omp.MessagingCodec
open VibeFs.Omp.NudgeRuntime
open VibeFs.Shell.RunnerBackground
open VibeFs.Shell.Dyn
module Dyn = VibeFs.Shell.Dyn
open VibeFs.Shell.FuzzyIteratorStore
open VibeFs.Shell.ReviewRuntime

let registerSessionLifecycle (pi: obj) (reviewStore: ReviewStore) : unit =
    pi?on(
        "before_agent_start",
        box(fun (event: obj) (ctx: obj) ->
            promise {
                let cwd = Dyn.str ctx "cwd"
                let sp = Dyn.get event "systemPrompt"
                let! patch = beforeAgentStart cwd sp
                return patch
            }))

    pi?on("tool_result", box(fun (event: obj) (ctx: obj) -> appendToolResultSyntax (Dyn.str ctx "cwd") event))

    pi?on(
        "agent_end",
        box(fun (_event: obj) (ctx: obj) ->
            match getSessionIdFromContext ctx with
            | None -> ()
            | Some sessionId ->
                if hasRunningRunnerJob sessionId then
                    if tryRunnerNudge sessionId then
                        pi?sendMessage(
                            createObj [
                                "customType", box "kunwei-runner-reminder"
                                "content", box runnerNudgePrompt
                                "display", box false
                            ],
                            createObj [ "triggerTurn", box true; "deliverAs", box "nextTurn" ])
                elif reviewStore.isReviewActive sessionId then
                    let hasPending =
                        let fn = Dyn.get ctx "hasPendingMessages"
                        Dyn.typeIs fn "function" && Dyn.truthy (Dyn.call0 fn)
                    let sm = Dyn.get ctx "sessionManager"
                    if not (Dyn.isNullish sm) && not hasPending then
                        let last = lastAssistantMessage sm
                        if tryLoopNudge sessionId last then
                            pi?sendMessage(
                                createObj [
                                    "customType", box "kunwei-loop-reminder"
                                    "content", box loopNudgePrompt
                                    "display", box false
                                ],
                                createObj [ "triggerTurn", box true; "deliverAs", box "nextTurn" ])))

    pi?on(
        "session_start",
        box(fun (_event: obj) (_ctx: obj) ->
            promise {
                let getActive = Dyn.get pi "getActiveTools"
                let active =
                    if Dyn.typeIs getActive "function" then
                        unbox<obj array> (Dyn.call0 getActive) |> Array.map string
                    else
                        [||]
                let filtered = filterOmpMainSessionActiveTools active
                if filtered.Length <> active.Length then
                    do! pi?setActiveTools(filtered) |> unbox<JS.Promise<unit>>
            }))

    pi?on(
        "session_shutdown",
        box(fun (_event: obj) (ctx: obj) ->
            promise {
                match getSessionIdFromContext ctx with
                | None -> ()
                | Some sessionId ->
                    clearNudgeSession sessionId
                    clearTypedIteratorScope globalIteratorStore sessionId
                    reviewStore.deactivateReview sessionId
                    do! cleanupRunnerJob sessionId
            }))