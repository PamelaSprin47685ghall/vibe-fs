module Wanxiangshu.Mux.BacklogSession

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.BacklogSessionCodec
open Wanxiangshu.Shell.RuntimeScope

type BacklogSession(scope: RuntimeScope) =
    let projection = scope.Projection

    member _.Host = mux

    member _.CaptureReport(callID: string, report: string) : unit =
        projection.CaptureReport(mux, callID, report)

    member this.ReplayBacklog(messages: Message<obj> list) : BacklogEntry list =
        replayBacklogWith mux (reportFromFlatPartWithProjection mux projection) messages

    member this.GetOrRebuildBacklog(sessionID: string, messages: Message<obj> list) : BacklogEntry list =
        if messages.Length > 0 then
            let backlog = this.ReplayBacklog messages
            projection.StoreBacklog(mux, sessionID, backlog)
            backlog
        else
            projection.TryGetBacklog(mux, sessionID) |> Option.defaultValue []
