module Wanxiangshu.Omp.MagicTodo

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Shell.BacklogSessionCodec
open Wanxiangshu.Shell.SessionProjectionStore
open Wanxiangshu.Shell.EventLogRuntimeStore

let private projection = ProjectionStore()

let backlogEntryFromTodoInput =
    Wanxiangshu.Shell.BacklogSessionCodec.backlogEntryFromTodoInput

type BacklogSession(host: Host) =
    let mutable workspaceRoot = ""

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
            (Wanxiangshu.Shell.BacklogSessionCodec.reportFromFlatPartWithProjection host projection)
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

let private shared (host: Host) : BacklogSession = BacklogSession host

let replayBacklogFor (host: Host) (messages: Message<obj> list) : BacklogEntry list =
    (shared host).ReplayBacklog messages

let replayBacklog (messages: Message<obj> list) : BacklogEntry list = replayBacklogFor omp messages
