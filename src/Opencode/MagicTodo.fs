module VibeFs.Opencode.MagicTodo

open Fable.Core.JsInterop
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Messaging
open VibeFs.Kernel.MagicCore
open VibeFs.Kernel.MagicTodo
open VibeFs.Shell.MagicSessionStore

/// Pull the `input` object off a tool part's state (host object, read here at
/// the FFI boundary; the Kernel reads only typed fields).
let inputOfPart (part: Part) : obj =
    match part with
    | ToolPart(_, _, Some state, _) -> state.input
    | _ -> null

let backlogReportFromTodoInput (host: Host) (input: obj) : string =
    let explicit = str input "completedWorkReport"
    if explicit.Trim() <> "" then explicit.Trim() else ""

type MagicSession(host: Host) =
    member _.Host = host

    member _.CaptureReport(callID: string, report: string) : unit =
        captureReport host callID report

    member _.TakeReport(callID: string) : string =
        takeReport host callID

    member this.BacklogInputForPart(fp: FlatPart) : obj =
        inputOfPart fp.part

    member this.ReplayBacklog(messages: Message list) : BacklogEntry list =
        let reportOf (fp: FlatPart) : string = backlogReportFromTodoInput host (this.BacklogInputForPart fp)
        replayBacklogWith host reportOf messages

    member this.GetOrRebuildBacklog(sessionID: string, messages: Message list) : BacklogEntry list =
        if messages.Length > 0 then
            let backlog = this.ReplayBacklog messages
            storeBacklog host sessionID backlog
            backlog
        else
            tryGetBacklog host sessionID |> Option.defaultValue []

let private shared (host: Host) : MagicSession = MagicSession host

let replayBacklogFor (host: Host) (messages: Message list) : BacklogEntry list =
    (shared host).ReplayBacklog messages

let replayBacklog (messages: Message list) : BacklogEntry list =
    replayBacklogFor opencode messages
