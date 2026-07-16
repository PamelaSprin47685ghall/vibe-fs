module Wanxiangshu.Hosts.Omp.MagicTodo

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.BacklogProjectionBuild
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Runtime.BacklogSessionCodec
open Wanxiangshu.Runtime.SessionProjectionStore
open Wanxiangshu.Runtime.EventLogRuntimeStore

let private projection = ProjectionStore()

let backlogEntryFromTodoInput =
    Wanxiangshu.Runtime.BacklogSessionCodec.backlogEntryFromTodoInput

let mutable workspaceRoot = ""

type BacklogSession(host: Host) =

    member _.Host = host
    member _.Projection = projection

    member _.WorkspaceRoot
        with get () = workspaceRoot
        and set (v) = workspaceRoot <- v

    member _.CaptureReport(callID: string, report: string) : unit =
        projection.CaptureReport(host, callID, report)

    member _.TakeReport(callID: string) : string = projection.TakeReport(host, callID)

    member this.ReplayBacklog(messages: Message<obj> list) : BacklogEntry list =
        replayBacklogWith
            host
            (Wanxiangshu.Runtime.BacklogSessionCodec.reportFromFlatPartWithProjection host projection)
            messages

    member this.GetOrRebuildBacklog(sessionID: string, messages: Message<obj> list) : BacklogEntry list =
        let state = getStore(workspaceRoot).GetSessionStateSync(sessionID)

        if not state.Backlog.IsEmpty then
            List.rev state.Backlog
        else
            let countTodoResults (mList: Message<obj> list) =
                mList
                |> List.sumBy (fun m -> m.parts |> List.filter (isTodoResultFor host) |> List.length)

            match projection.TryGetBacklog(host, sessionID) with
            | Some backlog ->
                if messages.Length > 0 && backlog.Length <> countTodoResults messages then
                    let nextBacklog = this.ReplayBacklog messages
                    projection.StoreBacklog(host, sessionID, nextBacklog)
                    nextBacklog
                else
                    backlog
            | None ->
                if messages.Length > 0 then
                    let backlog = this.ReplayBacklog messages
                    projection.StoreBacklog(host, sessionID, backlog)
                    backlog
                else
                    []

    member _.GetEventCount(sessionID: string) : int =
        getStore(workspaceRoot).GetSessionStateSync(sessionID).EventCount

let private shared (host: Host) : BacklogSession = BacklogSession host

let replayBacklogFor (host: Host) (messages: Message<obj> list) : BacklogEntry list =
    (shared host).ReplayBacklog messages

let replayBacklog (messages: Message<obj> list) : BacklogEntry list = replayBacklogFor omp messages
