module Wanxiangshu.Hosts.Opencode.SessionLifecycleHumanDispatch

open Fable.Core
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Messaging.OpencodeHostEvent
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection
open Wanxiangshu.Runtime.SubsessionEventRouter
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Runtime.Dispatch

/// A prompt is accepted before its host turn becomes idle. Keep the physical
/// dispatch occupied until that terminal fact arrives, then release it before
/// the actor can schedule a retry or a caller can start the next turn.
let settleChildDispatch (ctx: obj) (eventEnvelope: HostEventEnvelope option) : unit =
    match eventEnvelope with
    | Some env when
        env.EventType = "session.idle"
        || (env.EventType = "session.status"
            && resolveStatusValue (get env.Props "status") = "idle")
        || env.EventType = "session.error" ->
        let sid = getSessionID env.EventType env.Props
        let root = pluginDirectoryFromCtx ctx

        if sid <> "" && isChildSession root sid then
            let workspace =
                if root = "" then
                    Id.workspaceIdQuick "opencode-default"
                else
                    Id.workspaceIdQuick ("opencode:" + root)

            match sharedDispatchRegistry.TryGet workspace sid with
            | Some dispatcher ->
                match dispatcher.ActiveLogicalTurnId with
                | Some turnId when env.EventType = "session.error" ->
                    let errorObj = get env.Props "error"
                    dispatcher.FailByTurn turnId (opencodeErrorInput errorObj)
                    |> Promise.map ignore
                    |> Promise.start
                | Some turnId ->
                    dispatcher.CompleteByTurn turnId
                    |> Promise.map ignore
                    |> Promise.start
                | None -> ()
            | None -> ()
    | _ -> ()
