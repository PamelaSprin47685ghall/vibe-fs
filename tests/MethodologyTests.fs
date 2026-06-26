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
    check "enum: 54 values" (methodologyEnumValues.Length = 54)

let enumAllInCatalog () =
    methodologyEnumValues
    |> List.iter (fun v -> check ("catalog contains " + v) (methodologyCatalog.Contains(v)))

let catalogContainsKeyphrase () =
    check "catalog: contains keyphrase" (methodologyCatalog.Contains("Methodology catalog"))

let registryAlignsWithEnum () =
    check "registry: 54 schemas" (allSchemas.Length = 54)
    methodologyEnumValues
    |> List.iter (fun id ->
        check ("registry has " + id) (tryFindSchema id |> Option.isSome))
    allSchemas
    |> List.iter (fun s ->
        check ("schema has intent " + s.methodologyId) (
            s.fields |> List.exists (fun f -> f.name = intentFieldName && f.required))
        check ("schema has background " + s.methodologyId) (
            s.fields |> List.exists (fun f -> f.name = backgroundFieldName && f.required)))

let run () =
    todoWriteOutputExact ()
    enumCount ()
    enumAllInCatalog ()
    catalogContainsKeyphrase ()
    registryAlignsWithEnum ()