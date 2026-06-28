module Wanxiangshu.Methodology.Registry

open Wanxiangshu.Methodology.SchemaCommon
open Wanxiangshu.Methodology.Catalog

let allSchemas: MethodologySchema list = Catalog.all

let allToolSpecs: Wanxiangshu.Kernel.ToolCatalog.ToolSpec list =
    allSchemas |> List.map toToolCatalogSpec

let enumValues: string list =
    allSchemas |> List.map (fun s -> s.methodologyId)

let toolNames: string array =
    allSchemas |> List.map (fun s -> s.toolName) |> Array.ofList

let tryFindSchema methodologyId =
    allSchemas |> List.tryFind (fun s -> s.methodologyId = methodologyId)

let tryFindToolSpec methodologyId =
    tryFindSchema methodologyId |> Option.map toToolCatalogSpec
