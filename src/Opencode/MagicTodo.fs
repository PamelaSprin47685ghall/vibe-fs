module VibeFs.Opencode.MagicTodo

open Fable.Core.JsInterop
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Message
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.MagicCore
open VibeFs.Kernel.MagicTodo
open VibeFs.Shell.MagicSessionStore

type MagicSession(host: Host) =
    member _.Host = host

    member _.CaptureReport(callID: string, report: string) : unit =
        captureReport host callID report

    member _.TakeReport(callID: string) : string =
        takeReport host callID

    member this.BacklogInputForPart(fp: FlatPart) : obj =
        let input = partToolInput fp.part
        if host <> Mimocode then input
        else
            let cached = this.TakeReport(partCallID fp.part)
            if cached = "" then input
            elif isNullish input then createObj [ "completedWorkReport", box cached ]
            else
                input?("completedWorkReport") <- box cached
                input

    member this.ReplayBacklog(messages: obj array) : BacklogEntry list =
        VibeFs.Kernel.MagicTodo.replayBacklogWith host this.BacklogInputForPart messages

    member this.GetOrRebuildBacklog(sessionID: string, messages: obj array) : BacklogEntry list =
        if messages.Length > 0 then
            let backlog = this.ReplayBacklog messages
            storeBacklog host sessionID backlog
            backlog
        else
            tryGetBacklog host sessionID |> Option.defaultValue []

let private shared (host: Host) : MagicSession =
    MagicSession host

let captureCompletedWorkReport (callID: string) (report: string) : unit =
    (shared Mimocode).CaptureReport(callID, report)

let takeCompletedWorkReport (callID: string) : string =
    (shared Mimocode).TakeReport callID

let replayBacklogFor (host: Host) (messages: obj array) : BacklogEntry list =
    (shared host).ReplayBacklog messages

let replayBacklog (messages: obj array) : BacklogEntry list =
    replayBacklogFor opencode messages
