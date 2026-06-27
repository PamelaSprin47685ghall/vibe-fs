module Wanxiangshu.Omp.NudgeHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Shell.Dyn

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
            match tryLoopNudge sessionId last with
            | Some NudgeRunner ->
                pi?sendMessage(
                    createObj [
                        "customType", box "wanxiangshu-runner-reminder"
                        "content", box (runnerReminderContent ())
                        "display", box false
                    ],
                    createObj [ "triggerTurn", box true; "deliverAs", box "nextTurn" ])
            | Some NudgeLoop ->
                pi?sendMessage(
                    createObj [
                        "customType", box "wanxiangshu-loop-reminder"
                        "content", box (loopReminderContent ())
                        "display", box false
                    ],
                    createObj [ "triggerTurn", box true; "deliverAs", box "nextTurn" ])
            | _ -> ()
        elif not (Dyn.isNullish sm) && not hasPending then
            let last = lastAssistantMessage sm
            match tryTodoNudge sessionId sm last with
            | Some NudgeRunner ->
                pi?sendMessage(
                    createObj [
                        "customType", box "wanxiangshu-runner-reminder"
                        "content", box (runnerReminderContent ())
                        "display", box false
                    ],
                    createObj [ "triggerTurn", box true; "deliverAs", box "nextTurn" ])
            | Some NudgeTodo ->
                pi?sendMessage(
                    createObj [
                        "customType", box "wanxiangshu-todo-reminder"
                        "content", box (todoReminderContent ())
                        "display", box false
                    ],
                    createObj [ "triggerTurn", box true; "deliverAs", box "nextTurn" ])
            | _ -> ()
