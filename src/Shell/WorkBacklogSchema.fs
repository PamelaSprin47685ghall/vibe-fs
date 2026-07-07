module Wanxiangshu.Shell.WorkBacklogSchema

open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Shell.Dyn

let jsonStringProperty (description: string) : obj =
    createObj [ "type", box "string"; "description", box description ]

let jsonStringMinLengthProperty (minLength: int) (description: string) : obj =
    createObj [ "type", box "string"; "minLength", box minLength; "description", box description ]

let selectMethodologyProperty =
    createObj [
        "type", box "array"
        "description", box selectMethodologyFieldDescription
        "items", createObj [
            "type", box "string"
            "enum", box (Wanxiangshu.Methodology.Registry.enumValues.Value |> List.toArray)
        ]
        "minItems", box 1
    ]

let reportMinLength = 1024

let buildWorkBacklogSchema () : obj =
    let todoItem =
        createObj [
            "type", box "object"
            "properties", createObj [ "content", jsonStringProperty todoContentDesc; "status", jsonStringProperty todoStatusDesc; "priority", jsonStringProperty todoPriorityDesc ]
            "required", box [| box "content"; box "status"; box "priority" |]
        ]
    createObj [
        "type", box "object"
        "properties", createObj [
            "todos", createObj [ "type", box "array"; "description", box todosDesc; "items", todoItem ]
            "ahaMoments", jsonStringMinLengthProperty reportMinLength ahaMomentsDesc
            "changesAndReasons", jsonStringMinLengthProperty reportMinLength changesAndReasonsDesc
            "gotchas", jsonStringMinLengthProperty reportMinLength gotchasDesc
            "lessonsAndConventions", jsonStringMinLengthProperty reportMinLength lessonsAndConventionsDesc
            "plan", jsonStringMinLengthProperty reportMinLength planDesc
            "select_methodology", selectMethodologyProperty
        ]
        "required", box [| box "todos"; box "ahaMoments"; box "changesAndReasons"; box "gotchas"; box "lessonsAndConventions"; box "plan"; box "select_methodology" |]
    ]