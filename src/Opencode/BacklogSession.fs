module VibeFs.Opencode.BacklogSession

open Fable.Core.JsInterop
open VibeFs.Shell
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Messaging
open VibeFs.Kernel.BacklogProjectionCore
open VibeFs.Kernel.WorkBacklog
open VibeFs.Shell.BacklogSessionCodec
open VibeFs.Shell.RuntimeScope

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