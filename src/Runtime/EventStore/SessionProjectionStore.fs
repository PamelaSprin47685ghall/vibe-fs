module Wanxiangshu.Runtime.SessionProjectionStore

open Wanxiangshu.Kernel.HostTools

type ProjectionStore() =
    let mutable reportTables = Map.empty<Host, Map<string, string>>

    member this.CaptureReport(host: Host, callId: string, report: string) : unit =
        if callId <> "" && report.Trim() <> "" then
            let table = defaultArg (Map.tryFind host reportTables) Map.empty
            reportTables <- Map.add host (Map.add callId (report.Trim()) table) reportTables

    member this.TakeReport(host: Host, callId: string) : string =
        if callId = "" then
            ""
        else
            let table = defaultArg (Map.tryFind host reportTables) Map.empty

            match Map.tryFind callId table with
            | Some report ->
                reportTables <- Map.add host (Map.remove callId table) reportTables
                report
            | None -> ""

    member this.TryGetReport(host: Host, callId: string) : string option =
        if callId = "" then
            None
        else
            Map.tryFind host reportTables |> Option.bind (Map.tryFind callId)
