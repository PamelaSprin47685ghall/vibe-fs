module VibeFs.Mux.BacklogSession

open VibeFs.Kernel.HostTools
open VibeFs.Kernel.BacklogProjectionCore
open VibeFs.Kernel.WorkBacklog
open VibeFs.Kernel.Messaging
open VibeFs.Shell.BacklogSessionCodec
open VibeFs.Shell.RuntimeScope

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