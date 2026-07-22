module Wanxiangshu.Runtime.Tooling.ToolOutputToml

open Wanxiangshu.Kernel.ToolOutputInfoTypes
open Wanxiangshu.Runtime.Serialization.TomlValue
open Wanxiangshu.Runtime.Serialization.Toml

type SubagentReport =
    { iterator: string option
      body: string }

type BatchReport = private BatchReport of SubagentReport list

module BatchReport =
    let create (reports: SubagentReport list) : BatchReport option =
        if List.isEmpty reports then
            None
        else
            Some(BatchReport reports)

    let items (BatchReport reports) : SubagentReport list = reports

let toolOutputDocument (msg: ToolOutputMessage) : TomlValue =
    let mutable fields = []

    match msg.body with
    | Some b when b <> "" -> fields <- fields @ [ "body", String b ]
    | _ -> ()

    match msg.hint with
    | Some h when h <> "" -> fields <- fields @ [ "hint", String h ]
    | _ -> ()

    match msg.syntax with
    | Some s when s <> "" -> fields <- fields @ [ "syntax", String s ]
    | _ -> ()

    match msg.iterator with
    | Some i when i <> "" -> fields <- fields @ [ "iterator", String i ]
    | _ -> ()

    match msg.status with
    | Some st when st <> "" -> fields <- fields @ [ "status", String st ]
    | _ -> ()

    match msg.exitCode with
    | Some code -> fields <- fields @ [ "exit_code", Integer code ]
    | None -> ()

    Table fields

let renderToolOutput (msg: ToolOutputMessage) : string =
    match toolOutputDocument msg with
    | Table [] -> ""
    | doc -> stringify doc

let batchReportDocument (batch: BatchReport) : TomlValue =
    let reports = BatchReport.items batch

    let tables =
        reports
        |> List.map (fun r ->
            let mutable f = []

            match r.iterator with
            | Some iter -> f <- f @ [ "iterator", String iter ]
            | None -> ()

            f <- f @ [ "body", String r.body ]
            f)

    Table [ "reports", TableArray tables ]

let renderBatchReport (batch: BatchReport) : string = batchReportDocument batch |> stringify

type SearchResultItem =
    { title: string
      url: string
      content: string }

let searchResultsDocument (results: SearchResultItem list) : TomlValue =
    let tables =
        results
        |> List.map (fun r -> [ "title", String r.title; "url", String r.url; "content", String r.content ])

    Table [ "results", TableArray tables ]

let renderSearchResults (results: SearchResultItem list) : string =
    searchResultsDocument results |> stringify

type FetchResult =
    { title: string option
      byline: string option
      length: int option
      content: string option }

let fetchResultDocument (fetch: FetchResult) : TomlValue =
    let mutable fields = []

    match fetch.title with
    | Some t when t <> "" -> fields <- fields @ [ "title", String t ]
    | _ -> ()

    match fetch.byline with
    | Some b when b <> "" -> fields <- fields @ [ "byline", String b ]
    | _ -> ()

    match fetch.length with
    | Some l -> fields <- fields @ [ "length", Integer l ]
    | None -> ()

    match fetch.content with
    | Some c -> fields <- fields @ [ "content", String c ]
    | None -> ()

    if List.isEmpty fields then
        Table [ "title", String "" ]
    else
        Table fields

let renderFetchResult (fetch: FetchResult) : string = fetchResultDocument fetch |> stringify

type CapsItem = { label: string; content: string }

let capsDocument (caps: CapsItem list) : TomlValue =
    let tables =
        caps
        |> List.map (fun c -> [ "label", String c.label; "content", String c.content ])

    Table [ "capabilities", TableArray tables ]

let renderCaps (caps: CapsItem list) : string = capsDocument caps |> stringify

type SquadEventTomlView =
    { eventKind: string
      sessionId: string
      taskId: string option
      commitSha: string option
      message: string }

let squadEventDocument (view: SquadEventTomlView) : TomlValue =
    let mutable fields =
        [ "event_kind", String view.eventKind; "session_id", String view.sessionId ]

    match view.taskId with
    | Some tid -> fields <- fields @ [ "task_id", String tid ]
    | None -> ()

    match view.commitSha with
    | Some sha -> fields <- fields @ [ "commit_sha", String sha ]
    | None -> ()

    fields <- fields @ [ "message", String view.message ]
    Table fields

let renderSquadEvent (view: SquadEventTomlView) : string = squadEventDocument view |> stringify
