module Wanxiangshu.Shell.MessageTransformCore

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.BacklogProjection
open Wanxiangshu.Kernel.WorkBacklog
open Fable.Core

type BacklogSessionOps = {
    Host: Host
    GetOrRebuildBacklog: string -> Message<obj> list -> BacklogEntry list
    SyncBacklogFromEventLog: string -> string -> JS.Promise<unit>
}

let backlogSessionOpsFrom
    (host: Host)
    (getOrRebuildBacklog: string -> Message<obj> list -> BacklogEntry list)
    (syncBacklogFromEventLog: string -> string -> JS.Promise<unit>)
    : BacklogSessionOps =
    { Host = host; GetOrRebuildBacklog = getOrRebuildBacklog; SyncBacklogFromEventLog = syncBacklogFromEventLog }

let applyBacklogProjection
    (sessionID: string)
    (excluded: bool)
    (backlogSession: BacklogSessionOps)
    (cleaned: Message<obj> list)
    : Message<obj> list =
    if excluded then cleaned
    else
        let backlog = backlogSession.GetOrRebuildBacklog sessionID []
        projectBacklogFor backlogSession.Host cleaned backlog false sessionID