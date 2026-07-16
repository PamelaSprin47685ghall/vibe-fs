module Wanxiangshu.Tests.MethodologyRegistryTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Methodology.Schema
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Kernel.Methodology.Registry

let allEntriesCountIs54 () =
    equal "count" 54 allEntries.Value.Length

let shortDefinitionNonEmpty () =
    for e in allEntries.Value do
        check (e.methodologyId + " shortDefinition") (e.shortDefinition <> "")

let triggerWhenNonEmpty () =
    for e in allEntries.Value do
        check (e.methodologyId + " triggerWhen") (e.triggerWhen <> "")

let noteDescriptionNonEmpty () =
    for e in allEntries.Value do
        check (e.methodologyId + " noteDescription") (e.noteDescription <> "")

let meditatorRoleNonEmpty () =
    for e in allEntries.Value do
        check (e.methodologyId + " meditatorRole") (e.meditatorRole <> "")

let outputSectionsNonEmpty () =
    for e in allEntries.Value do
        check (e.methodologyId + " outputSections") (e.outputSections.Length > 0)

let allEntryIdsAreUnique () =
    let ids = allEntries.Value |> List.map (fun e -> e.methodologyId)
    equal "unique ids" ids (ids |> List.distinct)

let tryFindEntryFound () =
    match tryFindEntry "deduction" with
    | Some e -> equal "found" "deduction" e.methodologyId
    | None -> failwith "deduction not found"

let tryFindEntryNotFound () =
    equal "not found" None (tryFindEntry "nonexistent")

let enumValuesCountIs54 () =
    equal "enumValues count" 54 enumValues.Value.Length

let unifiedNoteDescriptionNonEmpty () =
    check "unifiedNoteDescription" (unifiedNoteDescription.Value <> "")

let catalogLazySameAsEagerListLength () =
    equal "Catalog.all.Length" 54 Catalog.all.Value.Length

let catalogLazyIsDeferredBeforeAccess () =
    let mutable evaluated = false

    let lazyVal =
        lazy
            (evaluated <- true
             42)

    check "lazy not created" (not lazyVal.IsValueCreated)
    check "lazy not evaluated" (not evaluated)
    let v = lazyVal.Value
    check "lazy created" lazyVal.IsValueCreated
    check "lazy evaluated" evaluated
    check "lazy value" (v = 42)
    check "allEntries.Value count" (allEntries.Value.Length = 54)
    check "enumValues.Value count" (enumValues.Value.Length = 54)
    check "enumValuesArray.Value length" (enumValuesArray.Value.Length = 54)
    check "unifiedNoteDescription.Value non-empty" (unifiedNoteDescription.Value <> "")

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
    catalogLazySameAsEagerListLength ()
    catalogLazyIsDeferredBeforeAccess ()
