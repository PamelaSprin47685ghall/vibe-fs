module Wanxiangshu.Hosts.Opencode.SessionLifecycleClose

open Fable.Core
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Runtime.Messaging.OpencodeHostEvent
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Hosts.Opencode.ChatHooksMessageIdDedup
open Wanxiangshu.Runtime.Dispatch
open Wanxiangshu.Runtime.Session.SessionActorRegistry

/// SessionClosed is a single domain command
/// that tears down every per-session side-effect at once,
/// not a "leak one at a time" pattern.  It must be safe
/// to call from the event handler's `finally` because the
/// session is being torn down by the host.  We dispatch
/// it through the unified DispatchRegistry so the
/// per-session mailbox, active dispatch, and pending
/// receipts are all released together.
let handleSessionClosed (ctx: obj) (sid: string) (eventEnvelope: HostEventEnvelope option) : unit =
    if
        eventEnvelope
        |> Option.exists (fun env ->
            env.EventType = "session.deleted"
            || env.EventType = "session.delete"
            || env.EventType = "session.remove"
            || env.EventType = "session.close")
    then
        let root = pluginDirectoryFromCtx ctx

        let ws =
            Wanxiangshu.Kernel.Primitives.Identity.Id.workspaceIdQuick (
                if root = "" then "opencode-default" else "opencode:" + root
            )

        let actorKey = if root = "" then "opencode-default" else "opencode:" + root
        sharedDispatchRegistry.NotifySessionClosed ws sid
        SessionActorRegistry.NotifyClosed actorKey sid
        forget sid
