module Wanxiangshu.Kernel.ToolCatalog

open Wanxiangshu.Kernel.ToolCatalog.ToolSpec
open Wanxiangshu.Kernel.ToolCatalog.Subagent
open Wanxiangshu.Kernel.ToolCatalog.Search
open Wanxiangshu.Kernel.ToolCatalog.Web
open Wanxiangshu.Kernel.ToolCatalog.Executor
open Wanxiangshu.Kernel.ToolCatalog.Review
open Wanxiangshu.Kernel.ToolCatalog.FileIO

type ToolSpec = Wanxiangshu.Kernel.ToolCatalog.ToolSpec.ToolSpec

let isFileEditTool = Wanxiangshu.Kernel.ToolCatalog.Classification.isFileEditTool

let all: ToolSpec list =
    [ coderSpec
      investigatorSpec
      meditatorSpec
      browserSpec
      continueSpec
      executorSpec
      fuzzyFindSpec
      fuzzyGrepSpec
      websearchSpec
      webfetchSpec
      submitReviewSpec
      returnReviewerSpec
      readSpec
      writeSpec
      executorWaitSpec
      executorAbortSpec ]

let private byName: Map<string, ToolSpec> =
    all |> List.map (fun spec -> spec.name, spec) |> Map.ofList

let specOf (name: string) : Result<ToolSpec, string> =
    match Map.tryFind name byName with
    | Some spec -> Ok spec
    | None -> Error(sprintf "ToolCatalog: unknown tool %s" name)

let paramDoc (name: string) (field: string) : Result<string, string> =
    match specOf name with
    | Error e -> Error e
    | Ok spec ->
        match Map.tryFind field spec.paramDocs with
        | Some doc -> Ok doc
        | None -> Error(sprintf "ToolCatalog: unknown param %s.%s" name field)

let description (name: string) : Result<string, string> =
    name |> specOf |> Result.map (fun spec -> spec.description)

let subagentRequiredKeys (toolName: string) : Result<string array, string> =
    toolName
    |> specOf
    |> Result.map (fun spec -> spec.requiredFields |> List.toArray)
