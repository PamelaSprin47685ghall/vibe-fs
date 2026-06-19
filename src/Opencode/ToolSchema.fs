module VibeFs.Opencode.ToolSchema

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.ToolCatalog

/// The opencode plugin SDK's `tool` factory + `tool.schema` (Zod-like) builder.
[<Import("tool", "@opencode-ai/plugin/tool")>]
let toolFactory : obj = jsNative
let schema : obj = Dyn.get toolFactory "schema"

/// `o.method()` and `o.method(arg)` - explicit object-first, no pipelines.
let call0 (o: obj) (method: string) : obj = o?(method)()
let call1 (o: obj) (method: string) (arg: obj) : obj = o?(method)(arg)

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
let strOpt (desc: string) : obj = call1 (call0 (str ()) "nullish") "describe" (box desc)

let intMinNullish (minVal: int) (desc: string) : obj =
    let n = call1 (call0 schema "number") "int" (box 0)
    let n = call1 n "min" (box minVal)
    call1 (call0 n "nullish") "describe" (box desc)

let boolOpt (desc: string) : obj = call1 (call0 (call0 schema "boolean") "nullish") "describe" (box desc)

let excludeOpt (desc: string) : obj =
    let s = str ()
    call1 (call0 (union' [| s; arr s |]) "nullish") "describe" (box desc)

let private schemaObject (shape: obj) : obj = call1 schema "object" shape

let private strictObject (shape: obj) : obj = call0 (schemaObject shape) "strict"

let private arrayMin (item: obj) (minCount: int) (desc: string) : obj =
    call1 (call1 (arr item) "min" (box minCount)) "describe" (box desc)

let coderIntentsSchema (desc: string) : obj =
    let fileField = strMin 1 "File path to modify."
    let guideField = strMin 1 "Implementation constraints for this file."
    let draftField = strOpt "Optional minimal draft for the coder to reference. Prefer leaving this empty; use only when strict quality needs a concrete sketch. No patch or special format required."
    let targetShape = strictObject (createObj [ "file", fileField; "guide", guideField; "draft", draftField ])
    let targetsField = arrayMin targetShape 1 "Non-empty per-file implementation guides."
    let objectiveField = strMin 1 "Concrete code-change goal for this subagent."
    let backgroundField = strMin 1 "Why this change is needed; prior findings and user context."
    let doNotTouchField = call1 (call0 (arr (strMin 1 "Do-not-touch path, symbol, or constraint.")) "optional") "describe" (box "Optional list of files, directories, symbols, or constraints this subagent must not modify.")
    let inner = strictObject (createObj [ "objective", objectiveField; "background", backgroundField; "do_not_touch", doNotTouchField; "targets", targetsField ])
    arrayMin inner 1 desc

let investigatorIntentsSchema (desc: string) : obj =
    let questionItem = strMin 1 "Question the report must answer."
    let questionsField = arrayMin questionItem 1 "Non-empty list of questions the report must answer explicitly."
    let entryItem = strMin 1 "Optional entry path, symbol, or file."
    let entriesField = call1 (call0 (arr entryItem) "optional") "describe" (box "Optional entry paths, symbols, or files to start from.")
    let objectiveField = strMin 1 "What to investigate in the codebase."
    let backgroundField = strMin 1 "Why this investigation is needed; blockers and prior context."
    let inner =
        strictObject (createObj [ "objective", objectiveField; "background", backgroundField; "questions", questionsField; "entries", entriesField ])
    arrayMin inner 1 desc

let uiParam : obj = call1 (call0 (str ()) "optional") "describe" (box "Internal: populated by hook")
let strArrayReq (desc: string) : obj = call1 (arr (strMin 1 "")) "describe" (box desc)
let strArrayOpt (desc: string) : obj = call1 (call0 (arr (strMin 1 "")) "optional") "describe" (box desc)

let numOpt (desc: string) : obj =
    let n = call0 schema "number"
    let n = call0 n "int"
    let n = call0 n "positive"
    call1 (call0 n "optional") "describe" (box desc)

let enumReq (values: string array) (desc: string) : obj =
    let e = call1 schema "enum" values
    call1 e "describe" (box desc)

let enumOpt (values: string array) (desc: string) : obj =
    let e = call1 schema "enum" values
    call1 (call0 e "optional") "describe" (box desc)

let obj (shape: obj) : obj = call1 schema "object" shape

let define (description: string) (args: obj) (execute: obj -> obj -> JS.Promise<string>) : obj =
    invokeTool toolFactory (box {| description = description; args = args; execute = execute |})

let coder = description "coder"

let investigator = description "investigator"

let meditator = description "meditator"

let browser = description "browser"

let executor = description "executor"

let fuzzyFind = description "fuzzy_find"

let fuzzyGrep = description "fuzzy_grep"

let websearch = description "websearch"

let webfetch = description "webfetch"

module Params =
    let private doc tool field = paramDoc tool field

    let coderIntents = doc "coder" "intents"
    let coderTdd = doc "coder" "tdd"
    let investigatorIntents = doc "investigator" "intents"
    let meditatorIntent = doc "meditator" "intent"
    let meditatorFiles = doc "meditator" "files"
    let browserIntent = doc "browser" "intent"
    let executorLanguage = doc "executor" "language"
    let executorProgram = doc "executor" "program"
    let executorDeps = doc "executor" "dependencies"
    let executorTimeout = doc "executor" "timeout_type"
    let fuzzyFindPattern = doc "fuzzy_find" "pattern"
    let fuzzyFindPath = doc "fuzzy_find" "path"
    let fuzzyFindLimit = doc "fuzzy_find" "limit"
    let fuzzyFindIterator = doc "fuzzy_find" "iterator"
    let fuzzyGrepPattern = doc "fuzzy_grep" "pattern"
    let fuzzyGrepPath = doc "fuzzy_grep" "path"
    let fuzzyGrepExclude = doc "fuzzy_grep" "exclude"
    let fuzzyGrepCaseSensitive = doc "fuzzy_grep" "caseSensitive"
    let fuzzyGrepContext = doc "fuzzy_grep" "context"
    let fuzzyGrepLimit = doc "fuzzy_grep" "limit"
    let fuzzyGrepIterator = doc "fuzzy_grep" "iterator"
    let websearchQuery = doc "websearch" "query"
    let websearchNumResults = doc "websearch" "numResults"
    let websearchWhatToSummarize = doc "websearch" "what_to_summarize"
    let webfetchUrl = doc "webfetch" "url"
    let webfetchExtractMain = doc "webfetch" "extract_main"
    let webfetchPreferLlmsTxt = doc "webfetch" "prefer_llms_txt"
    let webfetchPrompt = doc "webfetch" "prompt"
    let webfetchTimeout = doc "webfetch" "timeout"
