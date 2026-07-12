module Wanxiangshu.Shell.WorkBacklogSchema

open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Shell.Dyn

let jsonStringProperty (description: string) : obj =
    createObj [ "type", box "string"; "description", box description ]

let jsonStringMinLengthProperty (minLength: int) (description: string) : obj =
    createObj
        [ "type", box "string"
          "minLength", box minLength
          "description", box description ]

let selectMethodologyProperty =
    createObj
        [ "type", box "array"
          "description", box selectMethodologyFieldDescription
          "items",
          createObj
              [ "type", box "string"
                "enum", box (Wanxiangshu.Methodology.Registry.enumValues.Value |> List.toArray) ]
          "minItems", box 1 ]

let reportMinLength = 1024

let buildWorkBacklogSchema () : obj =
    let todoItem =
        createObj
            [ "type", box "object"
              "properties",
              createObj
                  [ "content", jsonStringProperty todoContentDesc
                    "status", jsonStringProperty todoStatusDesc
                    "priority", jsonStringProperty todoPriorityDesc ]
              "required", box [| box "content"; box "status"; box "priority" |] ]

    createObj
        [ "type", box "object"
          "properties",
          createObj
              [ "todos", createObj [ "type", box "array"; "description", box todosDesc; "items", todoItem ]
                "ahaMoments", jsonStringProperty ("MUST be at least 1024 characters. " + ahaMomentsDesc)
                "changesAndReasons", jsonStringProperty ("MUST be at least 1024 characters. " + changesAndReasonsDesc)
                "gotchas", jsonStringProperty ("MUST be at least 1024 characters. " + gotchasDesc)
                "lessonsAndConventions",
                jsonStringProperty ("MUST be at least 1024 characters. " + lessonsAndConventionsDesc)
                "plan", jsonStringProperty ("MUST be at least 1024 characters. " + planDesc)
                "select_methodology", selectMethodologyProperty ]
          "required", box [| box "todos"; box "select_methodology" |] ]
