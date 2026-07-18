module Wanxiangshu.Runtime.BacklogSession

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Runtime.BacklogProjectionBuild
open Wanxiangshu.Runtime.BacklogSessionCodec
open Wanxiangshu.Runtime.EventLogRuntimeStore
open Wanxiangshu.Runtime.RuntimeScope

type BacklogSession(host: Host, scope: RuntimeScope) =
    let projection = scope.Projection

    member _.Host = host
    member _.Projection = projection

    member _.WorkspaceRoot
        with get () = scope.WorkspaceRoot
        and set value = scope.WorkspaceRoot <- value

    member _.CaptureReport(callID: string, report: string) : unit =
        projection.CaptureReport(host, callID, report)

    member _.TakeReport(callID: string) : string = projection.TakeReport(host, callID)

    member this.ReplayBacklog(messages: Message<obj> list) : BacklogEntry list =
        replayBacklogWith host (reportFromFlatPartWithProjection host projection) messages

    member this.GetOrRebuildBacklog(sessionID: string, messages: Message<obj> list) : BacklogEntry list =
        let state = getStore(scope.WorkspaceRoot).GetSessionStateSync(sessionID)

        if not state.Backlog.IsEmpty then
            List.rev state.Backlog
        else
            let countTodoResults (mList: Message<obj> list) =
                mList
                |> List.sumBy (fun m -> m.parts |> List.filter (isTodoResultFor host) |> List.length)

            match projection.TryGetBacklog(host, sessionID) with
            | Some backlog ->
                if messages.IsEmpty || backlog.Length = countTodoResults messages then
                    backlog
                else
                    let nextBacklog = this.ReplayBacklog messages
                    projection.StoreBacklog(host, sessionID, nextBacklog)
                    nextBacklog
            | None when messages.IsEmpty -> []
            | None ->
                let backlog = this.ReplayBacklog messages
                projection.StoreBacklog(host, sessionID, backlog)
                backlog

    member _.GetEventCount(sessionID: string) : int =
        getStore(scope.WorkspaceRoot).GetSessionStateSync(sessionID).EventCount

let replayBacklogFor (host: Host) (scope: RuntimeScope) (messages: Message<obj> list) : BacklogEntry list =
    BacklogSession(host, scope).ReplayBacklog messages

let replayBacklog (scope: RuntimeScope) (messages: Message<obj> list) : BacklogEntry list =
    replayBacklogFor opencode scope messages
