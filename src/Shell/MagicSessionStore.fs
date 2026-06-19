module VibeFs.Shell.MagicSessionStore

open System.Collections.Generic
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.MagicCore

type private MagicSessionStoreState =
    { reportTables: Dictionary<Host, Dictionary<string, string>>
      backlogCaches: Dictionary<Host, Dictionary<string, BacklogEntry list>> }

type private MagicSessionStore(state: MagicSessionStoreState) =
    member private this.DictionaryFor(tables: Dictionary<Host, Dictionary<string, 'value>>, host: Host) : Dictionary<string, 'value> =
        match tables.TryGetValue host with
        | true, table -> table
        | false, _ ->
            let table = Dictionary<string, 'value>()
            tables.[host] <- table
            table

    member this.CaptureReport(host: Host, callId: string, report: string) : unit =
        if callId <> "" && report.Trim() <> "" then
            let reportByCall = this.DictionaryFor(state.reportTables, host)
            reportByCall.[callId] <- report.Trim()

    member this.TakeReport(host: Host, callId: string) : string =
        if callId = "" then ""
        else
            let reportByCall = this.DictionaryFor(state.reportTables, host)
            match reportByCall.TryGetValue callId with
            | true, report ->
                reportByCall.Remove callId |> ignore
                report
            | false, _ -> ""

    member this.StoreBacklog(host: Host, sessionId: string, backlog: BacklogEntry list) : unit =
        let backlogBySession = this.DictionaryFor(state.backlogCaches, host)
        backlogBySession.[sessionId] <- backlog

    member this.TryGetBacklog(host: Host, sessionId: string) : BacklogEntry list option =
        let backlogBySession = this.DictionaryFor(state.backlogCaches, host)
        match backlogBySession.TryGetValue sessionId with
        | true, backlog -> Some backlog
        | false, _ -> None

let private store =
    MagicSessionStore
        { reportTables = Dictionary<Host, Dictionary<string, string>>()
          backlogCaches = Dictionary<Host, Dictionary<string, BacklogEntry list>>() }

let captureReport (host: Host) (callId: string) (report: string) : unit =
    store.CaptureReport(host, callId, report)

let takeReport (host: Host) (callId: string) : string =
    store.TakeReport(host, callId)

let storeBacklog (host: Host) (sessionId: string) (backlog: BacklogEntry list) : unit =
    store.StoreBacklog(host, sessionId, backlog)

let tryGetBacklog (host: Host) (sessionId: string) : BacklogEntry list option =
    store.TryGetBacklog(host, sessionId)
