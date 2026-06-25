module VibeFs.Kernel.ToolCatalog

open VibeFs.Kernel.ToolCatalog.ToolSpec
open VibeFs.Kernel.ToolCatalog.Subagent
open VibeFs.Kernel.ToolCatalog.Search
open VibeFs.Kernel.ToolCatalog.Web
open VibeFs.Kernel.ToolCatalog.Executor
open VibeFs.Kernel.ToolCatalog.KnowledgeGraph
open VibeFs.Kernel.ToolCatalog.Review
open VibeFs.Kernel.ToolCatalog.FileIO

type ToolSpec = VibeFs.Kernel.ToolCatalog.ToolSpec.ToolSpec

let isFileEditTool = VibeFs.Kernel.ToolCatalog.Classification.isFileEditTool

let all: ToolSpec list =
    [ coderSpec
      investigatorSpec
      meditatorSpec
      browserSpec
      executorSpec
      fetchKnowledgeGraphSpec
      submitKnowledgeGraphSpec
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