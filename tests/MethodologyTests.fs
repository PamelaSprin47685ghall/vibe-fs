module Wanxiangshu.Tests.MethodologyTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Methodology.Registry
open Wanxiangshu.Methodology.SchemaCommon

let private hintFromTodoOutput (methodologies: string list) =
    match tryParse (todoWriteOutput methodologies false) with
    | Some msg ->
        msg.info
        |> List.choose (function InfoItem.Hint h -> Some h | _ -> None)
        |> String.concat " "
    | None -> ""

let todoWriteOutputExact () =
    check "todo envelope: empty methodologies" (hintFromTodoOutput [] = hintTodosUpdated)
    check "todo envelope: single methodology" (
        hintFromTodoOutput [ "first_principles" ] = hintMethodologyFollowup "first_principles")
    let multi =
        hintMethodologyFollowup "first_principles" + " " + hintMethodologyFollowup "deduction"
    check "todo envelope: multiple methodologies" (hintFromTodoOutput [ "first_principles"; "deduction" ] = multi)

let enumCount () =
    check "enum: 54 values" (enumValues.Value.Length = 54)

let enumAllInCatalog () =
    enumValues.Value
    |> List.iter (fun v -> check ("catalog contains " + v) (methodologyCatalog.Contains(v)))

let catalogContainsKeyphrase () =
    check "catalog: contains keyphrase" (methodologyCatalog.Contains("Methodology catalog"))

let registryEntriesWellFormed () =
    check "registry: 54 entries" (allEntries.Value.Length = 54)
    check "registry: enum count matches entries" (enumValues.Value.Length = allEntries.Value.Length)
    allEntries.Value
    |> List.iter (fun e ->
        check ("entry has shortDefinition " + e.methodologyId) (e.shortDefinition <> "")
        check ("entry has noteDescription " + e.methodologyId) (e.noteDescription <> ""))

let unifiedNoteDescriptionContainsAll () =
    check "unified note description non-empty" (unifiedNoteDescription.Value.Length > 0)
    for e in allEntries.Value do
        check ("unified note contains " + e.methodologyId) (unifiedNoteDescription.Value.Contains(e.methodologyId))

let run () =
    todoWriteOutputExact ()
    enumCount ()
    enumAllInCatalog ()
    catalogContainsKeyphrase ()
    registryEntriesWellFormed ()
    unifiedNoteDescriptionContainsAll ()
