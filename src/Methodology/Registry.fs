module Wanxiangshu.Methodology.Registry

open Wanxiangshu.Methodology.SchemaCommon
open Wanxiangshu.Methodology.Catalog

let allEntries: Lazy<MethodologyEntry list> = Catalog.all

let enumValues: Lazy<string list> =
    lazy (allEntries.Value |> List.map (fun e -> e.methodologyId))

let enumValuesArray: Lazy<string array> =
    lazy (enumValues.Value |> Array.ofList)

let unifiedNoteDescription: Lazy<string> =
    lazy (buildUnifiedNoteDescription allEntries.Value)

let tryFindEntry methodologyId =
    allEntries.Value |> List.tryFind (fun e -> e.methodologyId = methodologyId)
