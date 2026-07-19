module Wanxiangshu.Hosts.Mux.SlashCommands

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Runtime.LoopMessages
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.ReviewVerdict
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Hosts.Mux
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.ReviewEventWriter
open Wanxiangshu.Runtime.RuntimeScope

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

let private eventLogRootFromDeps (deps: obj) : string =
    if Dyn.isNullish deps then
        unbox<string> (nodeProcess?cwd ())
    else
        let d = Dyn.str deps "directory"
        if d <> "" then d else unbox<string> (nodeProcess?cwd ())

let private syncReviewTaskFromHistory
    (scope: RuntimeScope)
    (deps: obj)
    (reviewStore: ReviewStore)
    (workspaceId: string)
    : JS.Promise<string option> =
    promise {
        let root = eventLogRootFromDeps deps
        do! syncReviewFromEventLogDedicated reviewStore root workspaceId
        return reviewStore.getReviewTask workspaceId
    }

/// Build the /loop command (activate/cancel).
let private loopOnlyExecute
    (deps: obj)
    (reviewStore: ReviewStore)
    (scope: RuntimeScope)
    (workspaceId: WorkspaceId)
    (args: string)
    : JS.Promise<string> =
    let task = args.Trim()
    let workspaceIdStr = Id.workspaceIdValue workspaceId
    let root = eventLogRootFromDeps deps

    promise {
        scope.TriggerInit(root)
        do! scope.WaitInit()

        if task = "" then
            do! appendLoopCancelledOrFail root workspaceIdStr
            do! syncReviewFromEventLogDedicated reviewStore root workspaceIdStr
            return loopCancelledMessage
        else
            let! existingTask = syncReviewTaskFromHistory scope deps reviewStore workspaceIdStr

            if existingTask.IsSome then
                return "With-Review Mode is already active. Submit your work via submit_review."
            else
                do! appendLoopActivatedOrFail root workspaceIdStr task
                do! syncReviewFromEventLogDedicated reviewStore root workspaceIdStr

                return
                    buildLoopMessage
                        task
                        [ "With-Review Mode is active. Complete the task above, then call submit_review with:" ]
    }

let createSlashCommands
    (scope: RuntimeScope)
    (deps: obj)
    (toolNames: string array)
    (reviewStore: ReviewStore)
    : obj array =
    [| {| key = "loop"
          description =
           "Activate With-Review Mode or cancel it. Provide a task description to activate, or leave empty to cancel."
          inputHint = "<task description>"
          execute =
           System.Func<string, string, JS.Promise<string>>(fun workspaceIdStr args ->
               match Id.tryWorkspaceId workspaceIdStr with
               | None -> Promise.lift "Invalid workspaceId"
               | Some wid -> loopOnlyExecute deps reviewStore scope wid args) |}
       |> box |]
