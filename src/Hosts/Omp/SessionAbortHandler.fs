module Wanxiangshu.Hosts.Omp.SessionAbortHandler

open Fable.Core
open Fable.Core.JsInterop

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Runtime.Dispatch
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Hosts.Omp.ChildSession

/// `session.abort` / `stream.abort` / `session.error` all collapse to the
/// same outcome: in-flight review state must clear. Without this hook,
/// review state survives host-driven aborts and leaks across sessions.
let private sessionEndEventTypes =
    Set
        [ "session.abort"
          "stream.abort"
          "session.error"
          "session.delete"
          "session.close"
          "session.remove"
          "session.deleted"
          "session.interrupted" ]

let private handleSessionEnd (reviewStore: ReviewStore) (root: string) (sid: string) : JS.Promise<unit> =
    promise {
        if root <> "" then
            do! appendLoopCancelledOrFail root sid
            do! syncReviewFromEventLogDedicated reviewStore root sid

        Wanxiangshu.Hosts.Omp.NudgeRuntime.markSessionForceStopped sid

        // OMP does not register dispatchers in the DispatchRegistry
        // (no per-session mailbox yet), so there is nothing to tear down
        // here.  When OMP gains its own registry, add NotifySessionClosed.

        Wanxiangshu.Runtime.RunnerBackground.abortRunnerJobCore Wanxiangshu.Hosts.Omp.ExecutorTools.ompScope sid
    }

let private handleFallbackEvent
    (fallbackRuntime: FallbackRuntimeStore)
    (fallbackHandler: (obj -> JS.Promise<FallbackHookResult>) option)
    (event: obj)
    (evtType: string)
    (sid: string)
    : JS.Promise<unit> =
    promise {
        let isAssistantStream =
            evtType = "message.updated"
            && (let info = Dyn.get event "info"
                not (Dyn.isNullish info) && Dyn.str info "role" = "assistant")

        let isChild = isChildSession Wanxiangshu.Hosts.Omp.ExecutorTools.ompScope sid

        if isAssistantStream && not isChild then
            ()
        else
            match fallbackHandler with
            | Some handler ->
                let rawEvent =
                    createObj [ "event", box event; "props", box (createObj [ "sessionID", box sid ]) ]

                if sid <> "" then
                    fallbackRuntime.Update(sid, setEventHandlingActive true)

                try
                    let! _ = handler rawEvent
                    ()
                finally
                    if sid <> "" then
                        fallbackRuntime.Update(sid, setEventHandlingActive false)
            | None -> ()
    }

let registerAbortHandler
    (pi: obj)
    (reviewStore: ReviewStore)
    (fallbackRuntime: FallbackRuntimeStore)
    (fallbackHandler: (obj -> JS.Promise<FallbackHookResult>) option)
    : unit =
    let fallbackEventTypes =
        Set [ "session.busy"; "session.idle"; "message.updated"; "session.updated" ]

    pi?on (
        "event",
        box (fun (event: obj) (ctx: obj) ->
            promise {
                let evtType = Dyn.str event "type"
                let sidOpt = Wanxiangshu.Hosts.Omp.Codec.getSessionIdFromContext ctx

                match sidOpt with
                | None -> ()
                | Some sid ->
                    if sessionEndEventTypes.Contains evtType then
                        let root = Dyn.str ctx "cwd"
                        do! handleSessionEnd reviewStore root sid
                    elif fallbackEventTypes.Contains evtType then
                        do! handleFallbackEvent fallbackRuntime fallbackHandler event evtType sid
            })
    )
