module Wanxiangshu.Opencode.ToolSchema

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn

module Params = Wanxiangshu.Kernel.ToolCatalog.Params

/// The opencode plugin SDK's `tool` factory + `tool.schema` (Zod-like) builder.
[<Import("tool", "@opencode-ai/plugin/tool")>]
let toolFactory: obj = jsNative

let schema: obj = Dyn.get toolFactory "schema"

/// `o.method()` and `o.method(arg)` - explicit object-first, no pipelines.
let call0 (o: obj) (method: string) : obj = o?(method) ()
let call1 (o: obj) (method: string) (arg: obj) : obj = o?(method) (arg)

let private invokeTool (factory: obj) (config: obj) : obj = factory $ config

let private str () = call0 schema "string"
let arr (item: obj) : obj = call1 schema "array" item
let private tuple (items: obj array) : obj = call1 schema "tuple" items
let private union' (items: obj array) : obj = call1 schema "union" items

let strMin (minLen: int) (desc: string) : obj =
    call1 (call1 (str ()) "min" (box minLen)) "describe" (box desc)

let strMinNullish (minLen: int) (desc: string) : obj =
    call1 (call0 (call1 (str ()) "min" (box minLen)) "nullish") "describe" (box desc)

let strReq (desc: string) : obj = call1 (str ()) "describe" (box desc)

let strOpt (desc: string) : obj =
    call1 (call0 (str ()) "nullish") "describe" (box desc)

let intMinNullish (minVal: int) (desc: string) : obj =
    let n = call1 (call0 schema "number") "int" (box 0)
    let n = call1 n "min" (box minVal)
    call1 (call0 n "nullish") "describe" (box desc)

let boolOpt (desc: string) : obj =
    call1 (call0 (call0 schema "boolean") "nullish") "describe" (box desc)

/// Non-nullish optional boolean: `true | false | undefined`. `null` is invalid at the schema boundary.
let boolOptional (desc: string) : obj =
    call1 (call0 (call0 schema "boolean") "optional") "describe" (box desc)

let excludeOpt (desc: string) : obj =
    let s = str ()
    call1 (call0 (union' [| s; arr s |]) "nullish") "describe" (box desc)

let strArrayNullish (desc: string) : obj =
    let s = str ()
    call1 (call0 (arr s) "nullish") "describe" (box desc)

let private schemaObject (shape: obj) : obj = call1 schema "object" shape

let private strictObject (shape: obj) : obj = call0 (schemaObject shape) "strict"

/// Strict Zod object for a subagent tool; `requiredKeys` must match `ToolCatalog.subagentRequiredKeys` for that tool.
let subagentZodShape (requiredKeys: string array) (shape: obj) : obj =
    let shapeKeys = Set.ofArray (Dyn.keys shape)

    for k in requiredKeys do
        if not (Set.contains k shapeKeys) then
            failwithf "Opencode subagent Zod shape missing required field %s" k

    shape

let arrayMin (item: obj) (minCount: int) (desc: string) : obj =
    call1 (call1 (arr item) "min" (box minCount)) "describe" (box desc)

let coderIntentsSchema (desc: string) : obj =
    let fileField = strMin 1 coderTargetFileDesc
    let guideField = strMin 1 coderTargetGuideDesc
    let draftField = strOpt coderTargetDraftDesc

    let targetShape =
        strictObject (createObj [ "file", fileField; "guide", guideField; "draft", draftField ])

    let targetsField = arrayMin targetShape 1 coderTargetsDesc
    let objectiveField = strMin 1 coderObjectiveDesc
    let backgroundField = strMin 1 coderBackgroundDesc

    let doNotTouchField =
        call1 (call0 (arr (strMin 1 coderDoNotTouchItemDesc)) "optional") "describe" (box coderDoNotTouchDesc)

    let inner =
        strictObject (
            createObj
                [ "objective", objectiveField
                  "background", backgroundField
                  "do_not_touch", doNotTouchField
                  "targets", targetsField ]
        )

    arrayMin inner 1 desc

let investigatorIntentsSchema (desc: string) : obj =
    let questionItem = strMin 1 investigatorQuestionItemDesc
    let questionsField = arrayMin questionItem 1 investigatorQuestionsDesc
    let entryItem = strMin 1 investigatorEntryItemDesc

    let entriesField =
        call1 (call0 (arr entryItem) "optional") "describe" (box investigatorEntriesDesc)

    let objectiveField = strMin 1 investigatorObjectiveDesc
    let backgroundField = strMin 1 investigatorBackgroundDesc

    let inner =
        strictObject (
            createObj
                [ "objective", objectiveField
                  "background", backgroundField
                  "questions", questionsField
                  "entries", entriesField ]
        )

    arrayMin inner 1 desc

let uiParam: obj =
    call1
        (call0 (str ()) "optional")
        "describe"
        (box
            "Internal UI label. Visible to the LLM and UI, but the LLM must never fill it. Leave `ui_` unset; the tool.execute.before hook populates it automatically.")

let strArrayReq (desc: string) : obj =
    call1 (arr (strMin 1 "")) "describe" (box desc)

let strArrayOpt (desc: string) : obj =
    call1 (call0 (arr (strMin 1 "")) "optional") "describe" (box desc)

let numOpt (desc: string) : obj =
    let n = call0 schema "number"
    let n = call0 n "int"
    let n = call0 n "positive"
    call1 (call0 n "optional") "describe" (box desc)

let numReq (desc: string) : obj =
    let n = call0 schema "number"
    let n = call0 n "int"
    call1 n "describe" (box desc)

let enumReq (values: string array) (desc: string) : obj =
    let e = call1 schema "enum" values
    call1 e "describe" (box desc)

let enumOpt (values: string array) (desc: string) : obj =
    let e = call1 schema "enum" values
    call1 (call0 e "optional") "describe" (box desc)

let enumOptWithDefault (values: string array) (defaultValue: string) (desc: string) : obj =
    let e = call1 schema "enum" values
    let e = call1 e "default" (box defaultValue)
    call1 e "describe" (box desc)

let enumArrayMin (values: string array) (minCount: int) (desc: string) : obj =
    arrayMin (call1 schema "enum" values) minCount desc

let obj (shape: obj) : obj = call1 schema "object" shape

let define (description: string) (args: obj) (execute: obj -> obj -> JS.Promise<string>) : obj =
    invokeTool
        toolFactory
        (box
            {| description = description
               args = args
               execute = execute |})

let private toolDescription (name: string) : string =
    match Wanxiangshu.Kernel.ToolCatalog.description name with
    | Ok desc -> desc
    | Error e -> failwith e

let coder = toolDescription "coder"

let investigator = toolDescription "investigator"

let browser = toolDescription "browser"

let executor = toolDescription "executor"

let fuzzyFind = toolDescription "fuzzy_find"

let fuzzyGrep = toolDescription "fuzzy_grep"

let websearch = toolDescription "websearch"

let webfetch = toolDescription "webfetch"

let submitReview = toolDescription "submit_review"

let submitReviewResult = toolDescription "return_reviewer"

let continueSpec = toolDescription "continue"

let fuzzyContinue = toolDescription "fuzzy_continue"

let executorMode = Params.executorMode
