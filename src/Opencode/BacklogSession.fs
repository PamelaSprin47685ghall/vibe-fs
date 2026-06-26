module Wanxiangshu.Opencode.BacklogSession

open Fable.Core.JsInterop
open Wanxiangshu.Shell
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Shell.BacklogSessionCodec
open Wanxiangshu.Shell.RuntimeScope

let backlogReportFromTodoInput = BacklogSessionCodec.backlogReportFromTodoInput

type BacklogSession(host: Host, scope: RuntimeScope) =
    let projection = scope.Projection

    member _.Host = host

    member _.CaptureReport(callID: string, report: string) : unit =
        projection.CaptureReport(host, callID, report)

    member _.TakeReport(callID: string) : string =
        projection.TakeReport(host, callID)

    member this.ReplayBacklog(messages: Message<obj> list) : BacklogEntry list =
        replayBacklogWith host (BacklogSessionCodec.reportFromFlatPartWithProjection host projection) messages

    member this.GetOrRebuildBacklog(sessionID: string, messages: Message<obj> list) : BacklogEntry list =
        if messages.Length > 0 then
            let backlog = this.ReplayBacklog messages
            projection.StoreBacklog(host, sessionID, backlog)
            backlog
        else
            projection.TryGetBacklog(host, sessionID) |> Option.defaultValue []

let replayBacklogFor (host: Host) (scope: RuntimeScope) (messages: Message<obj> list) : BacklogEntry list =
    BacklogSession(host, scope).ReplayBacklog messages

let replayBacklog (scope: RuntimeScope) (messages: Message<obj> list) : BacklogEntry list =
    replayBacklogFor opencode scope messages