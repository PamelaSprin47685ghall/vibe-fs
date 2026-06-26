module Wanxiangshu.Omp.SessionLifecycleHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.TreeSitterKernel
open Wanxiangshu.Omp.ChildSession
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Omp.HookExecute
open Wanxiangshu.Omp.MessageTransform
open Wanxiangshu.Omp.OmpTestHooks
open Wanxiangshu.Omp.MessagingCodec
open Wanxiangshu.Omp.KnowledgeGraph.Runtime
open Wanxiangshu.Omp.NudgeRuntime
open Wanxiangshu.Omp.KnowledgeGraphTools
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Shell.RunnerBackground
open Wanxiangshu.Shell.Dyn
module Dyn = Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FuzzyIteratorStore
open Wanxiangshu.Shell.ReviewRuntime

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

let beforeAgentStartHandler (event: obj) (ctx: obj) : JS.Promise<obj> =
    promise {
        let cwd = Dyn.str ctx "cwd"
        let sp = Dyn.get event "systemPrompt"
        let! patch = beforeAgentStart cwd sp
        return patch
    }

let toolResultHandler (_pi: obj) (_reviewStore: ReviewStore) (kgRuntime: OmpKnowledgeGraphRuntime) (event: obj) (ctx: obj) : JS.Promise<unit> =
    promise {
        let toolName = Dyn.str event "toolName"
        let args = Dyn.get event "args"
        applyToolResultHook toolName args
        do! appendToolResultSyntax (Dyn.str ctx "cwd") event
        if toolName = todoWriteToolName omp then
            let methodologies =
                let raw = if Dyn.isNullish args then null else Dyn.get args "select_methodology"
                if Dyn.isNullish raw || not (Dyn.isArray raw) then []
                else
                    let rawArr = unbox<obj array> raw
                    rawArr |> Seq.map string |> List.ofSeq
            let content = Dyn.str event "content"
            if content <> "" then event?content <- todoWriteOutput methodologies true
        elif recordsToBookkeeper toolName && not (isReadOnlyExecutor toolName args) then
            let parentId = getSessionIdFromContext ctx |> Option.defaultValue ""
            if not (isChildSession parentId) then
                let cwd = Dyn.str ctx "cwd"
                let content = Dyn.str event "content"
                if cwd <> "" && content <> "" then
                    let input = if Dyn.isNullish args then "" else Fable.Core.JS.JSON.stringify args
                    kgRuntime.StartBookkeeperAppend(input, bodyForBookkeeper content, toolName, cwd)
                    event?content <- withBookkeepingHints content
    }

let agentEndHandler (pi: obj) (reviewStore: ReviewStore) (ctx: obj) : unit =
    match getSessionIdFromContext ctx with
    | None -> ()
    | Some sessionId ->
        let sm = Dyn.get ctx "sessionManager"
        let hasPending =
            let fn = Dyn.get ctx "hasPendingMessages"
            Dyn.typeIs fn "function" && Dyn.truthy (Dyn.call0 fn)
        if reviewStore.isReviewActive sessionId && not (Dyn.isNullish sm) && not hasPending then
            let last = lastAssistantMessage sm
            if tryLoopNudge sessionId last then
                pi?sendMessage(
                    createObj [
                        "customType", box "wanxiangshu-loop-reminder"
                        "content", box (loopReminderContent ())
                        "display", box false
                    ],
                    createObj [ "triggerTurn", box true; "deliverAs", box "nextTurn" ])
        elif not (Dyn.isNullish sm) && not hasPending then
            let last = lastAssistantMessage sm
            if tryTodoNudge sessionId sm last then
                pi?sendMessage(
                    createObj [
                        "customType", box "wanxiangshu-todo-reminder"
                        "content", box (todoReminderContent ())
                        "display", box false
                    ],
                    createObj [ "triggerTurn", box true; "deliverAs", box "nextTurn" ])

let sessionStartHandler (pi: obj) (kgRuntime: OmpKnowledgeGraphRuntime) (ctx: obj) : JS.Promise<unit> =
    promise {
        let getActive = Dyn.get pi "getActiveTools"
        let active =
            if Dyn.typeIs getActive "function" then
                let rawActive = Dyn.call0 getActive
                unbox<obj array> rawActive
                |> Microsoft.FSharp.Collections.Array.map string
            else
                [||]
        let filtered = filterOmpMainSessionActiveTools active
        if filtered.Length <> active.Length then
            do! pi?setActiveTools(filtered) |> unbox<JS.Promise<unit>>
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
