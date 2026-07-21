module Wanxiangshu.Hosts.Omp.TodoHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.NudgeToolFilter
open Wanxiangshu.Hosts.Omp.ExecutorTools
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.RunnerBackground
open Wanxiangshu.Runtime.ReviewEventWriter
open Wanxiangshu.Runtime.EventLogRuntimeSync
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Runtime.SubsessionPorts
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Kernel.Subsession.Types

module Dyn = Wanxiangshu.Runtime.Dyn

let sessionStartHandler (pi: obj) (reviewStore: ReviewStore) (ctx: obj) : JS.Promise<unit> =
    promise {
        do! applyActiveToolFilterForMainSession pi ctx
        let sessionId = getSessionIdFromContext ctx |> Option.defaultValue ""
        let cwd = Dyn.str ctx "cwd"

        if sessionId <> "" && cwd <> "" then
            do! syncReviewFromEventLogDedicated reviewStore cwd sessionId
    }

/// session_prompt: lightweight re-sync before each prompt to catch cross-session durable state changes.
let sessionPromptHandler (pi: obj) (reviewStore: ReviewStore) (ctx: obj) : JS.Promise<unit> =
    promise {
        let sessionId = getSessionIdFromContext ctx |> Option.defaultValue ""
        let cwd = Dyn.str ctx "cwd"

        if sessionId <> "" && cwd <> "" then
            do! syncReviewFromEventLogDedicated reviewStore cwd sessionId
    }

let sessionShutdownHandler
    (reviewStore: ReviewStore)
    (_fallbackRuntime: FallbackRuntimeStore)
    (ctx: obj)
    : JS.Promise<unit> =
    promise {
        match getSessionIdFromContext ctx with
        | None -> ()
        | Some sessionId ->
            let cwd = Dyn.str ctx "cwd"

            if cwd <> "" then
                do! appendLoopCancelledOrFail cwd sessionId
                do! syncReviewFromEventLogDedicated reviewStore cwd sessionId
                let sid = SessionId.create sessionId
                let eventStore = SubsessionEventStore.create cwd
                do! eventStore.Append(sid, [ PhysicalSessionClosed sid ])
                SubsessionActorRegistry.ClearPoison cwd sessionId
                SubsessionActorRegistry.Remove cwd sessionId

            do! cleanupRunnerJob ExecutorTools.ompScope sessionId
    }
