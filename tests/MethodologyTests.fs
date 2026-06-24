module VibeFs.Tests.MethodologyTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Methodology
open VibeFs.Kernel.ToolOutputInfo

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
    check "todo envelope: multiple methodologies" (
        hintFromTodoOutput [ "first_principles"; "deduction" ] = hintMethodologyFollowup "first_principles, deduction")

let enumCount () =
    check "enum: 54 values" (methodologyEnumValues.Length = 54)

let enumAllInCatalog () =
    methodologyEnumValues
    |> List.iter (fun v -> check ("catalog contains " + v) (methodologyCatalog.Contains(v)))

let catalogContainsKeyphrase () =
    check "catalog: contains keyphrase" (methodologyCatalog.Contains("Methodology catalog"))

let run () =
    todoWriteOutputExact ()
    enumCount ()
    enumAllInCatalog ()
    catalogContainsKeyphrase ()