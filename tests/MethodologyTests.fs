module VibeFs.Tests.MethodologyTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Methodology

let toolResultTextExact () =
    check "tool result: single methodology" (
        methodologyToolResultText [ "first_principles" ] = "Great! Now please explain how to apply [first_principles] to the work step.")
    check "tool result: multiple methodologies" (
        methodologyToolResultText [ "first_principles"; "deduction" ] = "Great! Now please explain how to apply [first_principles, deduction] to the work step.")

let todoResultTextExact () =
    check "todo result: empty" (todoResultText [] = "Todos updated.")
    check "todo result: single methodology" (
        todoResultText [ "first_principles" ] = "Great! Now please explain how to apply [first_principles] to the work step.")
    check "todo result: multiple methodologies" (
        todoResultText [ "first_principles"; "deduction" ] = "Great! Now please explain how to apply [first_principles, deduction] to the work step.")

let enumCount () =
    check "enum: 54 values" (methodologyEnumValues.Length = 54)

let enumAllInCatalog () =
    methodologyEnumValues
    |> List.iter (fun v -> check ("catalog contains " + v) (methodologyCatalog.Contains(v)))

let catalogContainsKeyphrase () =
    check "catalog: contains keyphrase" (methodologyCatalog.Contains("Methodology catalog"))

let run () =
    toolResultTextExact ()
    todoResultTextExact ()
    enumCount ()
    enumAllInCatalog ()
    catalogContainsKeyphrase ()
