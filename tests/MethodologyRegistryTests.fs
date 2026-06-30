module Wanxiangshu.Tests.MethodologyRegistryTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Methodology.SchemaCommon
open Wanxiangshu.Methodology.Registry

let allEntriesCountIs54 () =
    equal "count" 54 allEntries.Length

let shortDefinitionNonEmpty () =
    for e in allEntries do
        check (e.methodologyId + " shortDefinition") (e.shortDefinition <> "")

let triggerWhenNonEmpty () =
    for e in allEntries do
        check (e.methodologyId + " triggerWhen") (e.triggerWhen <> "")

let noteDescriptionNonEmpty () =
    for e in allEntries do
        check (e.methodologyId + " noteDescription") (e.noteDescription <> "")

let meditatorRoleNonEmpty () =
    for e in allEntries do
        check (e.methodologyId + " meditatorRole") (e.meditatorRole <> "")

let outputSectionsNonEmpty () =
    for e in allEntries do
        check (e.methodologyId + " outputSections") (e.outputSections.Length > 0)

let allEntryIdsAreUnique () =
    let ids = allEntries |> List.map (fun e -> e.methodologyId)
    equal "unique ids" ids (ids |> List.distinct)

let tryFindEntryFound () =
    match tryFindEntry "deduction" with
    | Some e -> equal "found" "deduction" e.methodologyId
    | None -> failwith "deduction not found"

let tryFindEntryNotFound () =
    equal "not found" None (tryFindEntry "nonexistent")

let enumValuesCountIs54 () =
    equal "enumValues count" 54 enumValues.Length

let unifiedNoteDescriptionNonEmpty () =
    check "unifiedNoteDescription" (unifiedNoteDescription <> "")

let run () =
    allEntriesCountIs54 ()
    shortDefinitionNonEmpty ()
    triggerWhenNonEmpty ()
    noteDescriptionNonEmpty ()
    meditatorRoleNonEmpty ()
    outputSectionsNonEmpty ()
    allEntryIdsAreUnique ()
    tryFindEntryFound ()
    tryFindEntryNotFound ()
    enumValuesCountIs54 ()
    unifiedNoteDescriptionNonEmpty ()
