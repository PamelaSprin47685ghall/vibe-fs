module Wanxiangshu.Methodology.Registry

open Wanxiangshu.Methodology.SchemaCommon
open Wanxiangshu.Methodology.Catalog

let allEntries: MethodologyEntry list = Catalog.all

let enumValues: string list =
    allEntries |> List.map (fun e -> e.methodologyId)

let enumValuesArray: string array =
    enumValues |> Array.ofList

let unifiedNoteDescription: string =
    buildUnifiedNoteDescription allEntries

let tryFindEntry methodologyId =
    allEntries |> List.tryFind (fun e -> e.methodologyId = methodologyId)
