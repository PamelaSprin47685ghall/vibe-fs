module Wanxiangshu.Hosts.Omp.TodoHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Runtime.PromptFragments
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.TreeSitterKernel
open Wanxiangshu.Hosts.Omp.NudgeToolFilter
open Wanxiangshu.Hosts.Omp.ChildSession
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.HookExecute
open Wanxiangshu.Hosts.Omp.MessageTransform
open Wanxiangshu.Hosts.Omp.ToolResultEvent
open Wanxiangshu.Hosts.Omp.MagicTodo
open Wanxiangshu.Hosts.Omp.MessagingCodec
open Wanxiangshu.Hosts.Omp.NudgeRuntime
open Wanxiangshu.Hosts.Omp.TodoStateManagement
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.RunnerBackground
open Wanxiangshu.Runtime.LivelockGuard
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.Dyn

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Runtime.FuzzyIteratorStore
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.BacklogProjectionBuild
open Wanxiangshu.Hosts.Omp.ExecutorTools
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.WorkBacklogToolsCodec
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Kernel.Subsession.Types

/// Shared BacklogSession bound to the OMP host.
let private backlogSession = BacklogSession omp

let sessionStartHandler (pi: obj) (reviewStore: ReviewStore) (ctx: obj) : JS.Promise<unit> =
    promise {
        do! applyActiveToolFilterForMainSession pi ctx
        let sessionId = getSessionIdFromContext ctx |> Option.defaultValue ""
        let cwd = Dyn.str ctx "cwd"

        if sessionId <> "" && cwd <> "" then
            do! Wanxiangshu.Runtime.EventLogRuntime.syncReviewFromEventLogDedicated reviewStore cwd sessionId

            do!
                Wanxiangshu.Runtime.EventLogRuntime.syncBacklogFromEventLogDedicated
                    omp
                    backlogSession.Projection
                    cwd
                    sessionId
    }

/// session_prompt: lightweight re-sync before each prompt to catch cross-session durable state changes.
let sessionPromptHandler (pi: obj) (reviewStore: ReviewStore) (ctx: obj) : JS.Promise<unit> =
    promise {
        let sessionId = getSessionIdFromContext ctx |> Option.defaultValue ""
        let cwd = Dyn.str ctx "cwd"

        if sessionId <> "" && cwd <> "" then
            do! Wanxiangshu.Runtime.EventLogRuntime.syncReviewFromEventLogDedicated reviewStore cwd sessionId

            do!
                Wanxiangshu.Runtime.EventLogRuntime.syncBacklogFromEventLogDedicated
                    omp
                    backlogSession.Projection
                    cwd
                    sessionId
    }

let sessionShutdownHandler (reviewStore: ReviewStore) (ctx: obj) : JS.Promise<unit> =
    promise {
        match getSessionIdFromContext ctx with
        | None -> ()
        | Some sessionId ->
            clearNudgeSession sessionId
            clearTypedIteratorScope ompScope.IteratorStore sessionId
            let cwd = Dyn.str ctx "cwd"

            if cwd <> "" then
                do! appendLoopCancelledOrFail cwd sessionId
                do! syncReviewFromEventLogDedicated reviewStore cwd sessionId
                let sid = SessionId.create sessionId
                let eventStore = SubsessionEventStore.create cwd
                do! eventStore.Append(sid, [ PhysicalSessionClosed sid ])
                SubsessionActorRegistry.ClearPoison cwd sessionId
                SubsessionActorRegistry.Remove cwd sessionId
                Wanxiangshu.Runtime.SubsessionPendingEvidence.SubsessionPendingEvidence.ForgetSession sessionId

            do! cleanupRunnerJob ExecutorTools.ompScope sessionId
            Wanxiangshu.Runtime.LivelockGuard.cleanup ExecutorTools.ompScope sessionId
            Wanxiangshu.Runtime.ToolHookRuntime.clearSessionCompliance sessionId
            Wanxiangshu.Runtime.ToolHookRuntime.closeSession sessionId
            ExecutorTools.ompScope.RemoveSessionQueue sessionId
            ExecutorTools.ompScope.RemoveTempFiles sessionId
    }
