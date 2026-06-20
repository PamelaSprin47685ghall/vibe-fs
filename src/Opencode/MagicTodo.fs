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

/// Extract the completed-work report from a todo/task input object. Moved here
/// from Kernel.MagicTodo because it reads host-object shapes (operation.action
/// etc.) via Dyn — this is the only legal site for that access.
let backlogReportFromTodoInput (host: Host) (input: obj) : string =
    let explicit = str input "completedWorkReport"
    if explicit.Trim() <> "" then explicit.Trim()
    elif host = Mimocode then
        let operation = get input "operation"
        if isNullish operation then ""
        else
            let eventSummary = str operation "event_summary"
            if eventSummary.Trim() <> "" then eventSummary.Trim()
            else
                match str operation "action" with
                | "create" ->
                    let summary = str operation "summary"
                    if summary.Trim() = "" then "" else "Created task: " + summary.Trim()
                | _ -> ""
    else ""

type MagicSession(host: Host) =
    member _.Host = host

    member _.CaptureReport(callID: string, report: string) : unit =
        captureReport host callID report

    member _.TakeReport(callID: string) : string =
        takeReport host callID

    member this.BacklogInputForPart(fp: FlatPart) : obj =
        let input = inputOfPart fp.part
        if host <> Mimocode then input
        else
            let cached = this.TakeReport(partCallID fp.part)
            if cached = "" then input
            elif isNullish input then createObj [ "completedWorkReport", box cached ]
            else
                input?("completedWorkReport") <- box cached
                input

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

let private shared (host: Host) : MagicSession =
    MagicSession host

let captureCompletedWorkReport (callID: string) (report: string) : unit =
    (shared Mimocode).CaptureReport(callID, report)

let takeCompletedWorkReport (callID: string) : string =
    (shared Mimocode).TakeReport callID

let replayBacklogFor (host: Host) (messages: Message list) : BacklogEntry list =
    (shared host).ReplayBacklog messages

let replayBacklog (messages: Message list) : BacklogEntry list =
    replayBacklogFor opencode messages
