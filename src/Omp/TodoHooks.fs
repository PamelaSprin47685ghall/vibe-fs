module Wanxiangshu.Omp.TodoHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.TreeSitterKernel
open Wanxiangshu.Omp.ChildSession
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Omp.HookExecute
open Wanxiangshu.Omp.MessageTransform
open Wanxiangshu.Omp.OmpTestHooks
open Wanxiangshu.Omp.ToolResultEvent
open Wanxiangshu.Omp.MagicTodo
open Wanxiangshu.Omp.MessagingCodec
open Wanxiangshu.Omp.NudgeRuntime
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Shell.RunnerBackground
open Wanxiangshu.Shell.LivelockGuard
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.BacklogProjectionCore
module Dyn = Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FuzzyIteratorStore
open Wanxiangshu.Shell.ReviewRuntime

/// Shared BacklogSession bound to the OMP host.
let private backlogSession = BacklogSession omp

/// Tools whose every user-facing invocation is durable enough to feed the
/// backlog as an input/output black box. Direct write
/// tools join the set via `isFileEditTool`; subagent and IO tools are listed
/// explicitly. Pure lookups (fuzzy_find/fuzzy_grep), the review tools themselves,
/// and host read tools never record.
let toolResultHandler (_pi: obj) (_reviewStore: ReviewStore) (event: obj) (ctx: obj) : JS.Promise<unit> =
    promise {
        let toolName = Dyn.str event "toolName"
        let args = getToolInput event
        let content = getToolResultText event
        let sessionId = getSessionIdFromContext ctx |> Option.defaultValue ""
        if sessionId <> "" && check sessionId toolName (JS.JSON.stringify args) content then
            setToolResultText event "livelock guard: repeated identical tool call with identical result"
        else
            applyToolResultHook toolName args
            do! appendToolResultSyntax (Dyn.str ctx "cwd") event
            if toolName = todoWriteToolName omp then
                let callId = getToolCallId event
                let input = getToolInput event
                let ahaMoments = if Dyn.isNullish input then "" else (Dyn.str input "ahaMoments").Trim()
                let changesAndReasons = if Dyn.isNullish input then "" else (Dyn.str input "changesAndReasons").Trim()
                let gotchas = if Dyn.isNullish input then "" else (Dyn.str input "gotchas").Trim()
                let lessonsAndConventions = if Dyn.isNullish input then "" else (Dyn.str input "lessonsAndConventions").Trim()
                let plan = if Dyn.isNullish input then "" else (Dyn.str input "plan").Trim()
                if (ahaMoments <> "" || changesAndReasons <> "" || gotchas <> "" || lessonsAndConventions <> "" || plan <> "") && callId <> "" then
                    let entry : BacklogEntry =
                        { ahaMoments = ahaMoments
                          changesAndReasons = changesAndReasons
                          gotchas = gotchas
                          lessonsAndConventions = lessonsAndConventions
                          plan = plan }
                    backlogSession.CaptureReport(callId, entry.plan)
                let methodologies =
                    let raw = if Dyn.isNullish args then null else Dyn.get args "select_methodology"
                    if Dyn.isNullish raw || not (Dyn.isArray raw) then []
                    else
                        let rawArr = unbox<obj array> raw
                        rawArr |> Seq.map string |> List.ofSeq
                let content = getToolResultText event
                if content <> "" then
                    setToolResultText event (todoWriteOutput methodologies true)
    }

let sessionStartHandler (pi: obj) (ctx: obj) : JS.Promise<unit> =
    promise {
        do! NudgeHooks.applyActiveToolFilterForMainSession pi ctx
    }

let sessionShutdownHandler (reviewStore: ReviewStore) (ctx: obj) : JS.Promise<unit> =
    promise {
        match getSessionIdFromContext ctx with
        | None -> ()
        | Some sessionId ->
            clearNudgeSession sessionId
            clearTypedIteratorScope globalIteratorStore sessionId
            reviewStore.deactivateReview sessionId
            do! cleanupRunnerJob sessionId
            Wanxiangshu.Shell.LivelockGuard.cleanup sessionId
    }
