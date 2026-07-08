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

let specOf (name: string) : ToolSpec =
    match Map.tryFind name byName with
    | Some spec -> spec
    | None -> failwithf "ToolCatalog: unknown tool %s" name

let paramDoc (name: string) (field: string) : string =
    let spec = specOf name

    match Map.tryFind field spec.paramDocs with
    | Some doc -> doc
    | None -> failwithf "ToolCatalog: unknown param %s.%s" name field

let description (name: string) : string = (specOf name).description

let subagentRequiredKeys (toolName: string) : string array =
    (specOf toolName).requiredFields |> List.toArray
