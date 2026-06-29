module Wanxiangshu.Shell.SessionProjectionStore

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.BacklogProjectionCore

type ProjectionStore() =
    let mutable reportTables = Map.empty<Host, Map<string, string>>
    let mutable backlogEntryTables = Map.empty<Host, Map<string, BacklogEntry>>
    let mutable backlogCaches = Map.empty<Host, Map<string, BacklogEntry list>>

    member this.CaptureReport(host: Host, callId: string, report: string) : unit =
        if callId <> "" && report.Trim() <> "" then
            let table = defaultArg (Map.tryFind host reportTables) Map.empty
            reportTables <- Map.add host (Map.add callId (report.Trim()) table) reportTables

    member this.TakeReport(host: Host, callId: string) : string =
        if callId = "" then ""
        else
            let table = defaultArg (Map.tryFind host reportTables) Map.empty
            match Map.tryFind callId table with
            | Some report ->
                reportTables <- Map.add host (Map.remove callId table) reportTables
                report
            | None -> ""

    member this.TryGetReport(host: Host, callId: string) : string option =
        if callId = "" then None
        else Map.tryFind host reportTables |> Option.bind (Map.tryFind callId)

    member this.CaptureBacklogEntry(host: Host, callId: string, entry: BacklogEntry) : unit =
        if callId <> "" then
            let table = defaultArg (Map.tryFind host backlogEntryTables) Map.empty
            backlogEntryTables <- Map.add host (Map.add callId entry table) backlogEntryTables

    member this.TryGetBacklogEntry(host: Host, callId: string) : BacklogEntry option =
        if callId = "" then None
        else Map.tryFind host backlogEntryTables |> Option.bind (Map.tryFind callId)

    member this.StoreBacklog(host: Host, sessionId: string, backlog: BacklogEntry list) : unit =
        let table = defaultArg (Map.tryFind host backlogCaches) Map.empty
        backlogCaches <- Map.add host (Map.add sessionId backlog table) backlogCaches

    member this.TryGetBacklog(host: Host, sessionId: string) : BacklogEntry list option =
        Map.tryFind host backlogCaches |> Option.bind (Map.tryFind sessionId)