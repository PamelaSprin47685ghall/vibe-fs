module VibeFs.Opencode.MagicTodo

open Fable.Core.JsInterop
open VibeFs.Shell
open VibeFs.Kernel.HostTools

open VibeFs.Kernel.Messaging
open VibeFs.Kernel.MagicCore
open VibeFs.Kernel.MagicTodo
open VibeFs.Shell.MagicSessionStore
open VibeFs.Shell.Dyn

/// Pull the `input` object off a tool part's state (host object, read here at
/// the FFI boundary; the Kernel reads only typed fields).
let inputOfPart (part: Part<obj>) : obj =
    match part with
    | ToolPart(_, _, Some state, _) -> state.input
    | _ -> null

/// Extract the `completedWorkReport` string from a todo tool `input` object.
let backlogReportFromTodoInput (_host: Host) (input: obj) : string =
    let raw = Dyn.get input "completedWorkReport"
    if Dyn.isNullish raw then "" else string raw

type MagicSession(host: Host) =
    member _.Host = host

    member _.CaptureReport(callID: string, report: string) : unit =
        captureReport host callID report

    member _.TakeReport(callID: string) : string =
        takeReport host callID

    member this.BacklogInputForPart(fp: FlatPart<obj>) : obj =
        inputOfPart fp.part

    member this.ReplayBacklog(messages: Message<obj> list) : BacklogEntry list =
        let reportOf (fp: FlatPart<obj>) : string = backlogReportFromTodoInput host (this.BacklogInputForPart fp)
        replayBacklogWith host reportOf messages

    member this.GetOrRebuildBacklog(sessionID: string, messages: Message<obj> list) : BacklogEntry list =
        if messages.Length > 0 then
            let backlog = this.ReplayBacklog messages
            storeBacklog host sessionID backlog
            backlog
        else
            tryGetBacklog host sessionID |> Option.defaultValue []

let private shared (host: Host) : MagicSession = MagicSession host

let replayBacklogFor (host: Host) (messages: Message<obj> list) : BacklogEntry list =
    (shared host).ReplayBacklog messages

let replayBacklog (messages: Message<obj> list) : BacklogEntry list =
    replayBacklogFor opencode messages
