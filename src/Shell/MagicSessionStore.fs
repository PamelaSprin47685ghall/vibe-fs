module VibeFs.Shell.MagicSessionStore

open VibeFs.Kernel.HostTools
open VibeFs.Kernel.MagicCore

type private MagicSessionStore() =
    let mutable reportTables = Map.empty<Host, Map<string, string>>
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

    member this.StoreBacklog(host: Host, sessionId: string, backlog: BacklogEntry list) : unit =
        let table = defaultArg (Map.tryFind host backlogCaches) Map.empty
        backlogCaches <- Map.add host (Map.add sessionId backlog table) backlogCaches

    member this.TryGetBacklog(host: Host, sessionId: string) : BacklogEntry list option =
        Map.tryFind host backlogCaches |> Option.bind (Map.tryFind sessionId)

let private store = MagicSessionStore()

let captureReport (host: Host) (callId: string) (report: string) : unit =
    store.CaptureReport(host, callId, report)

let takeReport (host: Host) (callId: string) : string =
    store.TakeReport(host, callId)

let tryGetReport (host: Host) (callId: string) : string option =
    store.TryGetReport(host, callId)

let storeBacklog (host: Host) (sessionId: string) (backlog: BacklogEntry list) : unit =
    store.StoreBacklog(host, sessionId, backlog)

let tryGetBacklog (host: Host) (sessionId: string) : BacklogEntry list option =
    store.TryGetBacklog(host, sessionId)
