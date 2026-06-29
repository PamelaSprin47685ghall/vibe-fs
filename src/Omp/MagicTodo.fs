module Wanxiangshu.Omp.MagicTodo

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Shell.BacklogSessionCodec
open Wanxiangshu.Shell.SessionProjectionStore

let private projection = ProjectionStore()

let backlogEntryFromTodoInput = Wanxiangshu.Shell.BacklogSessionCodec.backlogEntryFromTodoInput

type BacklogSession(host: Host) =
    member _.Host = host

    member _.CaptureReport(callID: string, report: string) : unit =
        projection.CaptureReport(host, callID, report)

    member _.TakeReport(callID: string) : string =
        projection.TakeReport(host, callID)

    member this.ReplayBacklog(messages: Message<obj> list) : BacklogEntry list =
        replayBacklogWith host (Wanxiangshu.Shell.BacklogSessionCodec.reportFromFlatPartWithProjection host projection) messages

    member this.GetOrRebuildBacklog(sessionID: string, messages: Message<obj> list) : BacklogEntry list =
        if messages.Length > 0 then
            let backlog = this.ReplayBacklog messages
            projection.StoreBacklog(host, sessionID, backlog)
            backlog
        else
            projection.TryGetBacklog(host, sessionID) |> Option.defaultValue []

let private shared (host: Host) : BacklogSession = BacklogSession host

let replayBacklogFor (host: Host) (messages: Message<obj> list) : BacklogEntry list =
    (shared host).ReplayBacklog messages

let replayBacklog (messages: Message<obj> list) : BacklogEntry list =
    replayBacklogFor omp messages