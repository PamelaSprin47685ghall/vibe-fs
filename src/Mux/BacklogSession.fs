module Wanxiangshu.Mux.BacklogSession

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.BacklogSessionCodec
open Wanxiangshu.Shell.EventLogRuntimeStore
open Wanxiangshu.Shell.RuntimeScope

type BacklogSession(scope: RuntimeScope) =
    let projection = scope.Projection

    member _.Host = mux

    member _.CaptureReport(callID: string, report: string) : unit =
        projection.CaptureReport(mux, callID, report)

    member this.ReplayBacklog(messages: Message<obj> list) : BacklogEntry list =
        replayBacklogWith mux (reportFromFlatPartWithProjection mux projection) messages

    member this.GetOrRebuildBacklog(sessionID: string, messages: Message<obj> list) : BacklogEntry list =
        let state = getStore(scope.WorkspaceRoot).GetSessionStateSync(sessionID)

        if not state.Backlog.IsEmpty then
            List.rev state.Backlog
        else
            let countTodoResults (mList: Message<obj> list) =
                mList
                |> List.sumBy (fun m -> m.parts |> List.filter (isTodoResultFor mux) |> List.length)

            match projection.TryGetBacklog(mux, sessionID) with
            | Some backlog ->
                if messages.Length > 0 && backlog.Length <> countTodoResults messages then
                    let nextBacklog = this.ReplayBacklog messages
                    projection.StoreBacklog(mux, sessionID, nextBacklog)
                    nextBacklog
                else
                    backlog
            | None ->
                if messages.Length > 0 then
                    let backlog = this.ReplayBacklog messages
                    projection.StoreBacklog(mux, sessionID, backlog)
                    backlog
                else
                    []

    member _.GetEventCount(sessionID: string) : int =
        getStore(scope.WorkspaceRoot).GetSessionStateSync(sessionID).EventCount

let replayBacklogFor (scope: RuntimeScope) (messages: Message<obj> list) : BacklogEntry list =
    BacklogSession(scope).ReplayBacklog messages

let replayBacklog (scope: RuntimeScope) (messages: Message<obj> list) : BacklogEntry list =
    replayBacklogFor scope messages
