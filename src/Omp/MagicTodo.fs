module VibeFs.Omp.MagicTodo

open VibeFs.Kernel.HostTools
open VibeFs.Kernel.BacklogProjectionCore
open VibeFs.Kernel.Messaging
open VibeFs.Kernel.WorkBacklog
open VibeFs.Shell.BacklogSessionCodec
open VibeFs.Shell.SessionProjectionStore

let private projection = ProjectionStore()

let backlogReportFromTodoInput = VibeFs.Shell.BacklogSessionCodec.backlogReportFromTodoInput

type BacklogSession(host: Host) =
    member _.Host = host

    member _.CaptureReport(callID: string, report: string) : unit =
        projection.CaptureReport(host, callID, report)

    member _.TakeReport(callID: string) : string =
        projection.TakeReport(host, callID)

    member this.ReplayBacklog(messages: Message<obj> list) : BacklogEntry list =
        replayBacklogWith host (VibeFs.Shell.BacklogSessionCodec.reportFromFlatPartWithProjection host projection) messages

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