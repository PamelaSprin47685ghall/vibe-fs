module Wanxiangshu.Runtime.Tooling.ToolOutputBatchToml

open Wanxiangshu.Kernel.ToolOutputInfoTypes
open Wanxiangshu.Runtime.Serialization.TomlValue
open Wanxiangshu.Runtime.Serialization.Toml

let private nonEmpty (s: string option) : string option =
    match s with
    | Some v when System.String.IsNullOrWhiteSpace v -> None
    | _ -> s

let private nonEmptyList (xs: string list) : string list =
    xs |> List.filter (System.String.IsNullOrWhiteSpace >> not)

type SubagentReport =
    { iterator: string option
      summary: string option
      error: FailureReason option
      findings: string list
      relatedFiles: string list
      relatedCode: string list }

type BatchReport = private BatchReport of SubagentReport list

module BatchReport =
    let create (reports: SubagentReport list) : BatchReport option =
        if List.isEmpty reports then
            None
        else
            Some(BatchReport reports)

    let items (BatchReport reports) : SubagentReport list = reports

let batchReportDocument (batch: BatchReport) : TomlValue =
    let reports = BatchReport.items batch

    let tables =
        reports
        |> List.map (fun r ->
            let mutable fields = []

            match r.iterator with
            | Some iter -> fields <- fields @ [ "iterator", String iter ]
            | None -> ()

            match r.error with
            | Some reason ->
                fields <-
                    fields
                    @ [ "error", String(failureReasonText reason)
                        "findings", StringArray []
                        "related_files", StringArray []
                        "related_code", StringArray [] ]
            | None ->
                match nonEmpty r.summary with
                | Some s -> fields <- fields @ [ "summary", String s ]
                | None -> ()

                fields <-
                    fields
                    @ [ "findings", StringArray(nonEmptyList r.findings)
                        "related_files", StringArray(nonEmptyList r.relatedFiles)
                        "related_code", StringArray(nonEmptyList r.relatedCode) ]

            fields)

    Table [ "reports", TableArray tables ]

let renderBatchReport (batch: BatchReport) : string = batchReportDocument batch |> stringify
