module Wanxiangshu.Shell.MessageTransformCore

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.BacklogProjection
open Wanxiangshu.Kernel.WorkBacklog
open Fable.Core

type BacklogSessionOps =
    { Host: Host
      GetOrRebuildBacklog: string -> Message<obj> list -> BacklogEntry list }

let backlogSessionOpsFrom
    (host: Host)
    (getOrRebuildBacklog: string -> Message<obj> list -> BacklogEntry list)
    : BacklogSessionOps =
    { Host = host
      GetOrRebuildBacklog = getOrRebuildBacklog }

let applyBacklogProjection
    (sessionID: string)
    (excluded: bool)
    (backlogSession: BacklogSessionOps)
    (cleaned: Message<obj> list)
    : Message<obj> list =
    if excluded then
        cleaned
    else
        let backlog = backlogSession.GetOrRebuildBacklog sessionID cleaned
        projectBacklogFor backlogSession.Host cleaned backlog false sessionID
