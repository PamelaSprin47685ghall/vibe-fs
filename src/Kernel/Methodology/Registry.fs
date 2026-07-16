module Wanxiangshu.Kernel.Methodology.Registry

open Wanxiangshu.Kernel.Methodology.Schema
open Wanxiangshu.Kernel.Methodology.Catalog

let allEntries: Lazy<MethodologyEntry list> = Catalog.all

let enumValues: Lazy<string list> =
    lazy (allEntries.Value |> List.map (fun e -> e.methodologyId))

let enumValuesArray: Lazy<string array> = lazy (enumValues.Value |> Array.ofList)

let unifiedNoteDescription: Lazy<string> =
    lazy (buildUnifiedNoteDescription allEntries.Value)

/// Precomputed lookup map for O(log N) entry retrieval.
let private entryMap: Lazy<Map<string, MethodologyEntry>> =
    lazy (allEntries.Value |> List.map (fun e -> e.methodologyId, e) |> Map.ofList)

let tryFindEntry methodologyId =
    entryMap.Value |> Map.tryFind methodologyId
