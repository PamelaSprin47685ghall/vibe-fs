module Wanxiangshu.Tests.MethodologyArgsCoverageTests

open Fable.Core
open Fable.Core.JsInterop
open System
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.MethodologyArgs
open Wanxiangshu.Kernel.Methodology.Schema
open Wanxiangshu.Hosts.Opencode.HookSchema
open Wanxiangshu.Runtime.Dyn

// ── Methodology.Args ───────────────────────────────────────────────────────

let methArgs () =
    // null → error
    match parse null with
    | Error msg -> check "null args error" (msg.Contains "missing")
    | Ok _ -> check "null args error" false
    // empty obj → all required fields missing
    match parse (createObj []) with
    | Error msg ->
        check "empty missing methodology" (msg.Contains "methodology")
        check "empty missing intent" (msg.Contains "intent")
        check "empty missing background" (msg.Contains "background")
        check "empty missing note" (msg.Contains "note")
    | Ok _ -> check "empty args error unexpected" false
    // partial obj → only missing fields reported
    match parse (createObj [ "methodology", box "m"; "intent", box "i" ]) with
    | Error msg ->
        check "partial missing background" (msg.Contains "background")
        check "partial missing note" (msg.Contains "note")
        check "partial not missing methodology" (not (msg.Contains "methodology"))
        check "partial not missing intent" (not (msg.Contains "intent"))
    | Ok _ -> check "partial args error unexpected" false
    // valid args → Ok record
    let okArgs =
        createObj
            [ "methodology", box "test"
              "intent", box "do stuff"
              "background", box "ctx"
              "note", box "my note" ]

    match parse okArgs with
    | Ok vs ->
        equal "methodology value" "test" vs.methodology
        equal "intent value" "do stuff" vs.intent
        equal "background value" "ctx" vs.background
        equal "note value" "my note" vs.note
    | Error msg -> check "ok parse error unexpected" false

// ── Methodology.SchemaCommon ───────────────────────────────────────────────

let methSchemaCommon () =
    let entry =
        { methodologyId = "first_principles"
          shortDefinition = "rebuild from facts"
          triggerWhen = "hard"
          noteDescription = "problem_statement, assumptions_to_strip, atomic_facts, rebuild_steps"
          meditatorRole = "analyst"
          outputSections = [ "Findings"; "Plan" ] }

    let prompt = renderMeditatorIntent entry "hi" "my background" "my note"
    check "prompt has methodology id" (prompt.Contains "first_principles")
    check "prompt has def" (prompt.Contains "rebuild from facts")
    check "prompt has trigger" (prompt.Contains "hard")
    check "prompt has role" (prompt.Contains "analyst")
    check "prompt has sections" (prompt.Contains "Findings")
    check "prompt has section order" (prompt.Contains "1. Findings")
    check "prompt has section 2" (prompt.Contains "2. Plan")
    check "prompt has intent" (prompt.Contains "hi")
    check "prompt has background" (prompt.Contains "my background")
    check "prompt has note" (prompt.Contains "my note")
    check "prompt has quiet room" (prompt.Contains "quiet room")

// ── Opencode.HookSchema ────────────────────────────────────────────────────

let hookSchemaSetUiLabel () =
    let target = createObj [ "file", box "a.fs"; "guide", box "g" ]

    let intentCoder =
        createObj [ "objective", box "do"; "background", box "ctx"; "targets", box [| target |] ]

    let argsCoder = createObj [ "intents", box [| intentCoder |] ]
    setUiLabel argsCoder "coder"
    check "coder label set" (string argsCoder?("ui_") = "do")

    let intentInv =
        createObj
            [ "objective", box "inv"
              "background", box "ctx"
              "questions", box [| box "q1" |]
              "entries", box [||] ]

    let argsInv = createObj [ "intents", box [| intentInv |] ]
    setUiLabel argsInv "investigator"
    check "investigator label set" (string argsInv?("ui_") = "inv")
    let argsOther = createObj []
    setUiLabel argsOther "other"
    check "other label not set" (isNullish (get argsOther "ui_"))

let hookSchemaStripUi () =
    let schema =
        createObj
            [ "type", box "object"
              "properties", createObj [ "x", box 1; "ui_", box 2 ]
              "required", box [| box "x"; box "ui_" |] ]

    let r = stripUiFromJsonSchema schema
    let props = get r "properties"
    check "ui removed from properties" (isNullish (get props "ui_"))
    check "x kept" (not (isNullish (get props "x")))
    let req = get r "required"
    check "ui removed from required" (not (Array.contains (box "ui_") (unbox<obj[]> req)))

let hookSchemaInjectWarnTdd () =
    let schema =
        createObj
            [ "properties", createObj [ "name", box (createObj [ "type", box "string" ]) ]
              "required", box [| box "name" |] ]

    injectWarnTddIntoJsonSchema schema |> ignore
    let props = get schema "properties"
    check "warn_tdd property added" (not (isNullish (get props "warn_tdd")))
    let req = unbox<obj[]> (get schema "required")
    check "warn_tdd NOT in required" (not (Array.contains (box "warn_tdd") req))
    let prop = get props "warn_tdd"
    check "warn_tdd soft-required" (truthy (get prop "required_"))
    let schema2 = createObj [ "warn_tdd", box "ignored" ]
    let r2 = injectWarnTddIntoJsonSchema schema2
    check "nullish returns non-null" (not (isNullish r2))

let hookSchemaMergeWorkBacklogReport () =
    let jsonSchema =
        createObj
            [ "type", box "object"
              "properties", createObj [ "task_id", box (createObj [ "type", box "string" ]) ]
              "required", box [| box "task_id" |] ]

    let r = mergeWorkBacklogReportIntoTaskSchema jsonSchema
    let props = get r "properties"
    check "ahaMoments added" (not (isNullish (get props "ahaMoments")))
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
