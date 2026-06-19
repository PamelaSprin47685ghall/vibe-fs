module VibeFs.Opencode.MagicTodo

open System.Collections.Generic
open Fable.Core.JsInterop
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Message
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.MagicCore
open VibeFs.Kernel.MagicTodo

let private completedWorkReportByCall = Dictionary<string, string>()

let captureCompletedWorkReport (callID: string) (report: string) =
    if callID <> "" && report.Trim() <> "" then completedWorkReportByCall.[callID] <- report.Trim()

let takeCompletedWorkReport (callID: string) : string =
    if callID = "" then ""
    else
        match completedWorkReportByCall.TryGetValue callID with
        | true, report ->
            completedWorkReportByCall.Remove callID |> ignore
            report
        | false, _ -> ""

let private backlogInputForPart (host: Host) (fp: FlatPart) : obj =
    let input = partToolInput fp.part
    if host <> Mimocode then input
    else
        let callID = partCallID fp.part
        let cached = takeCompletedWorkReport callID
        if cached = "" then input
        elif isNullish input then createObj [ "completedWorkReport", box cached ]
        else
            input?("completedWorkReport") <- box cached
            input

let replayBacklogFor (host: Host) (messages: obj array) : BacklogEntry list =
    VibeFs.Kernel.MagicTodo.replayBacklogWith host (backlogInputForPart host) messages

let replayBacklog (messages: obj array) : BacklogEntry list =
    replayBacklogFor opencode messages

type MagicSession(host: Host) =
    let cache = Dictionary<string, BacklogEntry list>()

    member _.Host = host

    member _.GetOrRebuildBacklog(sessionID: string, messages: obj array) : BacklogEntry list =
        if messages.Length > 0 then
            let backlog = replayBacklogFor host messages
            cache.[sessionID] <- backlog
            backlog
        else
            match cache.TryGetValue sessionID with
            | true, backlog -> backlog
            | false, _ -> []