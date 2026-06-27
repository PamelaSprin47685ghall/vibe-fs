module Wanxiangshu.Tests.CoverageFillMethodologyTests

open Fable.Core
open Fable.Core.JsInterop
open System
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Methodology.Args
open Wanxiangshu.Methodology.SchemaCommon
open Wanxiangshu.Opencode.HookSchema
open Wanxiangshu.Shell.Dyn

// ── Methodology.Args ───────────────────────────────────────────────────────

let methArgs () =
    let schema =
        { methodologyId = "test"
          toolName = "methodology_test"
          shortDefinition = "d"
          triggerWhen = "t"
          toolDescription = "desc"
          fields =
            [ { name = "intent"; description = "i"; required = true; kind = FieldKind.String; minArrayItems = 0 }
              { name = "background"; description = "b"; required = true; kind = FieldKind.String; minArrayItems = 0 }
              { name = "tags"; description = "t"; required = false; kind = FieldKind.StringArray; minArrayItems = 0 } ]
          meditatorRole = "r"
          outputSections = [] }
    match parse schema null with Error msg -> check "null args error" (msg.Contains "missing") | Ok _ -> check "null args error" false
    let reqSchema = { schema with fields = [ { name = "x"; description = ""; required = true; kind = FieldKind.String; minArrayItems = 0 } ] }
    match parse reqSchema (createObj []) with Error msg -> check "required missing" (msg.Contains "x") | Ok _ -> check "required missing" false
    let arrSchema = { schema with fields = [ { name = "arr"; description = ""; required = true; kind = FieldKind.StringArray; minArrayItems = 2 } ] }
    match parse arrSchema (createObj [ "arr", box [| box "a" |] ]) with Error msg -> check "array too short" (msg.Contains "arr") | Ok _ -> check "array too short" false
    let okArgs = createObj [ "intent", box "do stuff"; "background", box "ctx"; "tags", box [| box "a"; box "b" |] ]
    match parse schema okArgs with
    | Ok (vs, ar) ->
        check "ok values map" (Map.containsKey "intent" vs)
        check "ok array map" (Map.containsKey "tags" ar)
        equal "intent value" "do stuff" (Map.find "intent" vs)
        equal "tags values" ["a"; "b"] (Map.find "tags" ar)
    | Error msg -> check "ok parse error unexpected" false

// ── Methodology.SchemaCommon ───────────────────────────────────────────────

let methSchemaCommon () =
    let multi = "line1\nline2\nline3"
    let schema =
        { methodologyId = "first_principles"
          toolName = "methodology_first_principles"
          shortDefinition = "rebuild from facts"
          triggerWhen = "hard"
          toolDescription = "desc"
          fields =
            [ { name = "intent"; description = "i"; required = true; kind = FieldKind.String; minArrayItems = 0 }
              { name = "tags"; description = "t"; required = false; kind = FieldKind.StringArray; minArrayItems = 0 } ]
          meditatorRole = "analyst"
          outputSections = ["Findings"; "Plan"] }
    let values = Map.ofList [ "intent", "test"; "tags", "" ]
    let arrays = Map.ofList [ "tags", ["a"; "b"] ]
    let yaml = renderInputYaml schema values arrays
    check "yaml has methodology" (yaml.Contains "methodology: first_principles")
    check "yaml has intent" (yaml.Contains "intent: test")
    check "yaml array items" (yaml.Contains "- a")
    let prompt = renderMeditatorIntent schema "inputs:\n  intent: hi\n"
    check "prompt has def" (prompt.Contains "rebuild from facts")
    check "prompt has role" (prompt.Contains "analyst")
    check "prompt has sections" (prompt.Contains "Findings")
    let spec = toToolCatalogSpec schema
    equal "spec name" "methodology_first_principles" spec.name
    check "spec has required" (List.contains "intent" spec.requiredFields)

// ── Opencode.HookSchema ────────────────────────────────────────────────────

let hookSchemaSetUiLabel () =
    let target = createObj [ "file", box "a.fs"; "guide", box "g" ]
    let intentCoder = createObj [ "objective", box "do"; "background", box "ctx"; "targets", box [| target |] ]
    let argsCoder = createObj [ "intents", box [| intentCoder |] ]
    setUiLabel argsCoder "coder"
    check "coder label set" (string argsCoder?("_ui") = "do")
    let intentInv = createObj [ "objective", box "inv"; "background", box "ctx"; "questions", box [| box "q1" |]; "entries", box [||] ]
    let argsInv = createObj [ "intents", box [| intentInv |] ]
    setUiLabel argsInv "investigator"
    check "investigator label set" (string argsInv?("_ui") = "inv")
    let argsOther = createObj []
    setUiLabel argsOther "other"
    check "other label not set" (isNullish (get argsOther "_ui"))

let hookSchemaStripUi () =
    let schema = createObj [ "type", box "object"; "properties", createObj [ "x", box 1; "_ui", box 2 ]; "required", box [| box "x"; box "_ui" |] ]
    let r = stripUiFromJsonSchema schema
    let props = get r "properties"
    check "ui removed from properties" (isNullish (get props "_ui"))
    check "x kept" (not (isNullish (get props "x")))
    let req = get r "required"
    check "ui removed from required" (not (Array.contains (box "_ui") (unbox<obj[]> req)))

let hookSchemaInjectWarnTdd () =
    let schema = createObj [ "properties", createObj [ "name", box (createObj [ "type", box "string" ]) ]; "required", box [| box "name" |] ]
    injectWarnTddIntoJsonSchema schema |> ignore
    let props = get schema "properties"
    check "warn_tdd property added" (not (isNullish (get props "warn_tdd")))
    let req = unbox<obj[]> (get schema "required")
    check "warn_tdd in required" (Array.contains (box "warn_tdd") req)
    let schema2 = createObj [ "warn_tdd", box "ignored" ]
    let r2 = injectWarnTddIntoJsonSchema schema2
    check "nullish returns non-null" (not (isNullish r2))

let hookSchemaMergeWorkBacklogReport () =
    let jsonSchema = createObj [ "type", box "object"; "properties", createObj [ "task_id", box (createObj [ "type", box "string" ]) ]; "required", box [| box "task_id" |] ]
    let r = mergeWorkBacklogReportIntoTaskSchema jsonSchema
    let props = get r "properties"
    check "completedWorkReport added" (not (isNullish (get props "completedWorkReport")))
    check "select_methodology added" (not (isNullish (get props "select_methodology")))
    check "task_id removed from properties" (isNullish (get props "task_id"))
    let req = unbox<obj[]> (get r "required")
    check "task_id removed from required" (not (Array.contains (box "task_id") req))
    check "select_methodology in required" (Array.contains (box "select_methodology") req)


let run () =
    methArgs ()
    methSchemaCommon ()
    hookSchemaSetUiLabel ()
    hookSchemaStripUi ()
    hookSchemaInjectWarnTdd ()
    hookSchemaMergeWorkBacklogReport ()
