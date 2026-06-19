module VibeFs.Opencode.MagicTodo

open System.Collections.Generic
open Fable.Core.JsInterop
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Message
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.MagicCore
open VibeFs.Kernel.MagicTodo

/// Per-session magic-todo state: backlog projection cache plus the by-callID
/// `completedWorkReport` side-table that bridges Mimocode's `tool.execute.before`
/// (which strips the report so the strict task schema validates) and
/// `tool.execute.after` (which restores it for backlog replay). The report
/// table is keyed by host so any `MagicSession host` instance — including the
/// one the host plugin owns and the one module-level helpers route through —
/// observes the same captures regardless of who allocated it.
let private reportTables = Dictionary<Host, Dictionary<string, string>>()

let private reportTableFor (host: Host) : Dictionary<string, string> =
    match reportTables.TryGetValue host with
    | true, table -> table
    | false, _ ->
        let table = Dictionary<string, string>()
        reportTables.[host] <- table
        table

type MagicSession(host: Host) =
    let cache = Dictionary<string, BacklogEntry list>()
    let reportByCall = reportTableFor host

    member _.Host = host

    member _.CaptureReport(callID: string, report: string) : unit =
        if callID <> "" && report.Trim() <> "" then reportByCall.[callID] <- report.Trim()

    member _.TakeReport(callID: string) : string =
        if callID = "" then ""
        else
            match reportByCall.TryGetValue callID with
            | true, report ->
                reportByCall.Remove callID |> ignore
                report
            | false, _ -> ""

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
            cache.[sessionID] <- backlog
            backlog
        else
            match cache.TryGetValue sessionID with
            | true, backlog -> backlog
            | false, _ -> []

/// Shared sessions keyed by host. Hooks that don't receive a `MagicSession`
/// instance — namely `tool.execute.before/after` — must still capture and
/// restore reports across calls; routing them through `shared host` keeps the
/// state inside `MagicSession` while preserving the existing hook signatures.
let private sharedByHost = Dictionary<Host, MagicSession>()

let shared (host: Host) : MagicSession =
    match sharedByHost.TryGetValue host with
    | true, session -> session
    | false, _ ->
        let session = MagicSession host
        sharedByHost.[host] <- session
        session

let captureCompletedWorkReport (callID: string) (report: string) : unit =
    (shared Mimocode).CaptureReport(callID, report)

let takeCompletedWorkReport (callID: string) : string =
    (shared Mimocode).TakeReport callID

let replayBacklogFor (host: Host) (messages: obj array) : BacklogEntry list =
    (shared host).ReplayBacklog messages

let replayBacklog (messages: obj array) : BacklogEntry list =
    replayBacklogFor opencode messages