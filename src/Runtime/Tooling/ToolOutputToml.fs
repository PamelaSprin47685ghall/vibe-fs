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

let infoItemTable =
    function
    | InfoItem.Hint h -> Table [ "kind", String "hint"; "text", String h ]
    | InfoItem.Syntax s -> Table [ "kind", String "syntax"; "text", String s ]
    | InfoItem.Iterator i -> Table [ "kind", String "iterator"; "text", String i ]
    | InfoItem.Status s -> Table [ "kind", String "status"; "text", String s ]
    | InfoItem.ExitCode n -> Table [ "kind", String "exit_code"; "number", Integer n ]

let toolOutputDocument (msg: ToolOutputMessage) : TomlValue =
    let mutable fields = [ "body", String msg.body ]

    if not (List.isEmpty msg.info) then
        let tables =
            msg.info
            |> List.rev
            |> List.map (
                infoItemTable
                >> function
                    | Table t -> t
                    | _ -> []
            )

        fields <- fields @ [ "info", TableArray tables ]

    Table fields

let renderToolOutput (msg: ToolOutputMessage) : string =
    if List.isEmpty msg.info && msg.body = "" then
        ""
    else
        toolOutputDocument msg |> stringify

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
    { title: string
      byline: string option
      length: int
      content: string }

let fetchResultDocument (fetch: FetchResult) : TomlValue =
    let mutable fields = [ "title", String fetch.title ]

    match fetch.byline with
    | Some b -> fields <- fields @ [ "byline", String b ]
    | None -> ()

    fields <- fields @ [ "length", Integer fetch.length; "content", String fetch.content ]
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
