module Wanxiangshu.Runtime.MuxJsonSchema

open Fable.Core.JsInterop
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Runtime.JsonSchemaBuilders

let muxStrReq = jsonStrReq
let muxStrOpt = jsonStrProp
let muxStrArrayReq = jsonStrArrayReq
let muxStrArrayOpt = jsonStrArrayOpt
let muxObjectSchema = jsonObjectSchema

let muxCoderIntentsSchema (intentsDesc: string) : obj =
    let targetItem =
        muxObjectSchema
            (createObj
                [ "file", muxStrReq coderTargetFileDesc
                  "guide", muxStrReq coderTargetGuideDesc
                  "draft", muxStrOpt coderTargetDraftDesc ])
            [| "file"; "guide" |]

    let intentItem =
        muxObjectSchema
            (createObj
                [ "objective", muxStrReq coderObjectiveDesc
                  "background", muxStrReq coderBackgroundDesc
                  "do_not_touch", muxStrArrayOpt coderDoNotTouchDesc
                  "targets",
                  createObj
                      [ "type", box "array"
                        "minItems", box 1
                        "items", targetItem
                        "description", box coderTargetsDesc ] ])
            [| "objective"; "background"; "targets" |]

    createObj
        [ "type", box "array"
          "minItems", box 1
          "items", intentItem
          "description", box intentsDesc ]

let muxInspectorIntentsSchema (intentsDesc: string) : obj =
    let intentItem =
        muxObjectSchema
            (createObj
                [ "objective", muxStrReq inspectorObjectiveDesc
                  "background", muxStrReq inspectorBackgroundDesc
                  "questions", muxStrArrayReq inspectorQuestionsDesc
                  "entries", muxStrArrayOpt inspectorEntriesDesc ])
            [| "objective"; "background"; "questions" |]

    createObj
        [ "type", box "array"
          "minItems", box 1
          "items", intentItem
          "description", box intentsDesc ]
