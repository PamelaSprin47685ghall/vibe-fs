module Wanxiangshu.Hosts.Omp.NudgeReminderDispatch

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Hosts.Omp.NudgeToolFilter
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Runtime.PromptFragments
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Runtime.Nudge.NudgeDerivation
open Wanxiangshu.Kernel.Nudge.NudgeSnapshotSource
open Wanxiangshu.Kernel.TreeSitterKernel
open Wanxiangshu.Hosts.Omp.ChildSession
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.ExecutorTools
open Wanxiangshu.Hosts.Omp.HookExecute
open Wanxiangshu.Hosts.Omp.MessageTransform
open Wanxiangshu.Hosts.Omp.ToolResultEvent
open Wanxiangshu.Hosts.Omp.MessagingCodec
open Wanxiangshu.Hosts.Omp.NudgeRuntime
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Runtime.RunnerBackground
open Wanxiangshu.Runtime.LivelockGuard
open Wanxiangshu.Runtime.RuntimeScope

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Kernel.EventSourcing.Fold

let sendNudgeReminder (pi: IPi) (action: NudgeAction) (snapshot: SessionSnapshot) : JS.Promise<unit> =
    promise {
        if pi.sendMessage.IsSome then
            let call (msg: obj) (opts: obj) =
                let r = pi?sendMessage (msg, opts)
                if Dyn.isNullish r then Promise.lift () else unbox r

            match action with
            | NudgeRunner ->
                do!
                    call
                        (createObj
                            [ "customType", box "wanxiangshu-runner-reminder"
                              "content", box (runnerReminderContent ())
                              "display", box false ])
                        (createObj [ "triggerTurn", box true; "deliverAs", box "nextTurn" ])
            | NudgeLoop ->
                do!
                    call
                        (createObj
                            [ "customType", box "wanxiangshu-loop-reminder"
                              "content", box (loopReminderContent snapshot.todos)
                              "display", box false ])
                        (createObj [ "triggerTurn", box true; "deliverAs", box "nextTurn" ])
            | NudgeTodo ->
                do!
                    call
                        (createObj
                            [ "customType", box "wanxiangshu-todo-reminder"
                              "content", box (todoReminderContent snapshot.todos)
                              "display", box false ])
                        (createObj [ "triggerTurn", box true; "deliverAs", box "nextTurn" ])
            | NudgeNone -> ()
    }

let resolveAgentLocal (ctx: obj) : string =
    let sm = Dyn.get ctx "sessionManager"

    if Dyn.isNullish sm then
        "manager"
    else
        let name = Dyn.str sm "agentName"
        if name <> "" then name else "manager"
