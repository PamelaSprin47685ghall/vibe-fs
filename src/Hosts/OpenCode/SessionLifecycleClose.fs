module Wanxiangshu.Hosts.Opencode.SessionLifecycleClose

open Fable.Core
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Runtime.Messaging.OpencodeHostEvent
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Hosts.Opencode.ChatHooksMessageIdDedup
open Wanxiangshu.Runtime.Dispatch
open Wanxiangshu.Runtime.Session.SessionActorRegistry

let private actorKeyFromCtx (ctx: obj) : string =
    let root = pluginDirectoryFromCtx ctx
    if root = "" then "opencode-default" else "opencode:" + root

let private workspaceIdFromCtx (ctx: obj) =
    let root = pluginDirectoryFromCtx ctx
    Id.workspaceIdQuick (if root = "" then "opencode-default" else "opencode:" + root)

/// Side-effect cleanup for a closed physical session.
/// Safe to call from inside the SessionActor handler — does NOT Post back.
let finalizeSessionClosed (ctx: obj) (sid: string) : unit =
    if sid <> "" then
        sharedDispatchRegistry.NotifySessionClosed (workspaceIdFromCtx ctx) sid
        SessionActorRegistry.Remove (actorKeyFromCtx ctx) sid
        forget sid

/// Host session.deleted/close envelope path: enqueue SessionClosed via registry
/// removal only after the actor has drained the closed fact externally.
let handleSessionClosed (ctx: obj) (sid: string) (eventEnvelope: HostEventEnvelope option) : unit =
    if
        eventEnvelope
        |> Option.exists (fun env ->
            env.EventType = "session.deleted"
            || env.EventType = "session.delete"
            || env.EventType = "session.remove"
            || env.EventType = "session.close")
    then
        // Prefer actor path: Post is done by SessionLifecycleEvents for decoded facts.
        // Fallback when called without actor admission (legacy hooks): finalize directly.
        finalizeSessionClosed ctx sid
