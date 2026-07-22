module Wanxiangshu.Tests.MethodologyArgsCoverageTests

open Fable.Core
open Fable.Core.JsInterop
open System
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.MethodologyArgs
open Wanxiangshu.Runtime.SubagentPrompts
open Wanxiangshu.Runtime.SubagentSummarizerPrompts
open Wanxiangshu.Kernel.Methodology.Schema
open Wanxiangshu.Hosts.Opencode.HookSchemaDecoration
open Wanxiangshu.Hosts.Opencode.HookSchemaDecode
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
    check "prompt has methodology_id key" (prompt.Contains "methodology_id")
    check "prompt has methodology id value" (prompt.Contains "first_principles")
    check "prompt has definition key" (prompt.Contains "definition")
    check "prompt has def value" (prompt.Contains "rebuild from facts")
    check "prompt has trigger key" (prompt.Contains "trigger")
    check "prompt has trigger value" (prompt.Contains "hard")
    check "prompt has role key" (prompt.Contains "role")
    check "prompt has role value" (prompt.Contains "analyst")
    check "prompt has sections" (prompt.Contains "Findings")
    check "prompt has section outcome 1" (prompt.Contains "section_1" && prompt.Contains "Findings")
    check "prompt has section outcome 2" (prompt.Contains "section_2" && prompt.Contains "Plan")
    check "no OUTPUT_SECTION prose key" (not (prompt.Contains "OUTPUT_SECTION_"))
    check "prompt has intent" (prompt.Contains "hi")
    check "prompt has background" (prompt.Contains "my background")
    check "prompt has note" (prompt.Contains "my note")
    check
        "prompt has quiet room"
        (prompt.Contains "NO_TOOLS" || prompt.ToLowerInvariant().Contains "do not call tools")
    check "no METHODOLOGY_ID prose key" (not (prompt.Contains "METHODOLOGY_ID:"))
    check "no DEFINITION prose key" (not (prompt.Contains "DEFINITION:"))
    check "kind is methodology" (prompt.Contains "methodology")

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
    setUiLabel argsInv "inspector"
    check "inspector label set" (string argsInv?("ui_") = "inv")
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

let run () =
    methArgs ()
    methSchemaCommon ()
    hookSchemaSetUiLabel ()
    hookSchemaStripUi ()
