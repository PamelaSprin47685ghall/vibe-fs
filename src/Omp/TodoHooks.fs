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
open Wanxiangshu.Omp.KnowledgeGraph.Runtime
open Wanxiangshu.Omp.NudgeRuntime
open Wanxiangshu.Omp.KnowledgeGraphTools
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Shell.RunnerBackground
open Wanxiangshu.Shell.LivelockGuard
open Wanxiangshu.Shell.Dyn
module Dyn = Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FuzzyIteratorStore
open Wanxiangshu.Shell.ReviewRuntime

/// Shared BacklogSession bound to the OMP host.
let private backlogSession = BacklogSession omp

/// Tools whose every user-facing invocation is durable enough to feed the
/// knowledge graph bookkeeper as an input/output black box. Direct write
/// tools join the set via `isFileEditTool`; subagent and IO tools are listed
/// explicitly. Pure lookups (fuzzy_find/fuzzy_grep), the knowledge graph /
/// review tools themselves, and host read tools never record.
let bookkeepingSubagentTools =
    Set [ "coder"; "investigator"; "meditator"; "browser"; "executor"; "websearch"; "webfetch"; "write"; "apply_patch"; "patch" ]

let recordsToBookkeeper (toolName: string) : bool =
    isFileEditTool toolName || Set.contains toolName bookkeepingSubagentTools

/// Read-only executor runs (file reads, greps) are durable enough to surface
/// in the conversation but not stable enough to feed the long-term bookkeeper.
/// Only `mode = "ro"` qualifies; "rw" must record so executor work survives
/// knowledge graph compaction.
let isReadOnlyExecutor (toolName: string) (args: obj) : bool =
    toolName = "executor" && Dyn.str args "mode" = "ro"

let toolResultHandler (_pi: obj) (_reviewStore: ReviewStore) (kgRuntime: OmpKnowledgeGraphRuntime) (event: obj) (ctx: obj) : JS.Promise<unit> =
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
                let raw = if Dyn.isNullish input then "" else string (Dyn.get input "completedWorkReport")
                let report = raw.Trim()
                if report <> "" && callId <> "" then
                    backlogSession.CaptureReport(callId, report)
                let methodologies =
                    let raw = if Dyn.isNullish args then null else Dyn.get args "select_methodology"
                    if Dyn.isNullish raw || not (Dyn.isArray raw) then []
                    else
                        let rawArr = unbox<obj array> raw
                        rawArr |> Seq.map string |> List.ofSeq
                let content = getToolResultText event
                if content <> "" then
                    setToolResultText event (todoWriteOutput methodologies true)
            elif recordsToBookkeeper toolName && not (isReadOnlyExecutor toolName args) then
                let parentId = getSessionIdFromContext ctx |> Option.defaultValue ""
                if not (isChildSession parentId) then
                    let cwd = Dyn.str ctx "cwd"
                    let content = getToolResultText event
                    if cwd <> "" && content <> "" then
                        let input = if Dyn.isNullish args then "" else Fable.Core.JS.JSON.stringify args
                        kgRuntime.StartBookkeeperAppend(input, bodyForBookkeeper content, toolName, cwd)
                        setToolResultText event (withBookkeepingHints content)
    }

let sessionStartHandler (pi: obj) (kgRuntime: OmpKnowledgeGraphRuntime) (ctx: obj) : JS.Promise<unit> =
    promise {
        do! NudgeHooks.applyActiveToolFilterForMainSession pi ctx
        let cwd = Dyn.str ctx "cwd"
        ensureKnowledgeGraphTools pi kgRuntime cwd
    }

let sessionShutdownHandler (reviewStore: ReviewStore) (kgRuntime: OmpKnowledgeGraphRuntime) (ctx: obj) : JS.Promise<unit> =
    promise {
        match getSessionIdFromContext ctx with
        | None -> ()
        | Some sessionId ->
            clearNudgeSession sessionId
            clearTypedIteratorScope globalIteratorStore sessionId
            reviewStore.deactivateReview sessionId
            kgRuntime.DeleteJob sessionId
            do! cleanupRunnerJob sessionId
    }
