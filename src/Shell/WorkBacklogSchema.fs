module Wanxiangshu.Shell.WorkBacklogSchema

open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Shell.Dyn

let jsonStringProperty (description: string) : obj =
    createObj [ "type", box "string"; "description", box description ]

let selectMethodologyProperty =
    createObj [
        "type", box "array"
        "description", box selectMethodologyFieldDescription
        "items", createObj [
            "type", box "string"
            "enum", box (List.toArray Wanxiangshu.Methodology.Registry.enumValues)
        ]
        "minItems", box 1
    ]

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
            "completedWorkReport", jsonStringProperty reportDesc
            "select_methodology", selectMethodologyProperty
        ]
        "required", box [| box "todos"; box "completedWorkReport"; box "select_methodology" |]
    ]