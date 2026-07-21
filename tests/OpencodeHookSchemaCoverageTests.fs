module Wanxiangshu.Tests.OpencodeHookSchemaCoverageTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Hosts.Opencode.HookSchemaDecoration
open Wanxiangshu.Hosts.Opencode.HookSchemaDecode
open Wanxiangshu.Kernel.WarnTdd
open Wanxiangshu.Runtime.Dyn

module Dyn = Wanxiangshu.Runtime.Dyn

[<Import("Schema", "effect")>]
let private effectSchemaNs: obj = jsNative

[<Emit("$0[$1]($2)")>]
let private callMethod1 (o: obj) (name: string) (arg: obj) : obj = jsNative

// ── setUiLabel ────────────────────────────────────────────────────────────────

let opencodeHookSchemaSetUiLabelCoder () =
    let args =
        createObj
            [ "intents",
              box
                  [| box (
                         createObj
                             [ "objective", box "Fix bug"
                               "background", box "reason"
                               "targets", box [| createObj [ "file", box "src/a.fs"; "guide", box "fix it" ] |]
                               "do_not_touch", box [||] ]
                     ) |] ]

    setUiLabel args "coder"
    check "coder ui_ set" (not (Dyn.isNullish (Dyn.get args "ui_")))

let opencodeHookSchemaSetUiLabelInspector () =
    let args =
        createObj
            [ "intents",
              box
                  [| box
                         {| objective = "Inspect"
                            background = "reason"
                            questions = [| "Q1" |]
                            entries = [||] |} |] ]

    setUiLabel args "inspector"
    check "inspector ui_ set" (not (Dyn.isNullish (Dyn.get args "ui_")))

let opencodeHookSchemaSetUiLabelOther () =
    let args =
        createObj
            [ "intents",
              box
                  [| box
                         {| objective = "Fix bug"
                            background = "reason"
                            targets = [||]
                            do_not_touch = [||] |} |] ]

    setUiLabel args "other"
    check "other ui_ not set" (Dyn.isNullish (Dyn.get args "ui_"))

// ── stripUiFromJsonSchema ─────────────────────────────────────────────────────

let opencodeHookSchemaStripUiFromJsonSchemaWithUi () =
    let schema =
        createObj
            [ "type", box "object"
              "properties", createObj [ "name", box (createObj []); "ui_", box (createObj []) ] ]

    let result = stripUiFromJsonSchema schema
    check "type preserved" (Dyn.str result "type" = "object")
    check "ui_ removed" (Dyn.isNullish (Dyn.get (Dyn.get result "properties") "ui_"))
    check "name kept" (not (Dyn.isNullish (Dyn.get (Dyn.get result "properties") "name")))

let opencodeHookSchemaStripUiFromJsonSchemaNoUi () =
    let schema =
        createObj [ "type", box "object"; "properties", createObj [ "name", box (createObj []) ] ]

    let result = stripUiFromJsonSchema schema
    check "type preserved no ui_" (Dyn.str result "type" = "object")
    check "name still present" (not (Dyn.isNullish (Dyn.get (Dyn.get result "properties") "name")))

let opencodeHookSchemaStripUiFromJsonSchemaNull () =
    let result = stripUiFromJsonSchema null
    check "null returns null" (isNull result)

// ── rewriteToolJsonSchema ─────────────────────────────────────────────────────

let opencodeHookSchemaRewriteToolJsonSchemaJsonSchema () =
    let mutable capturedKey = ""

    let setKey (o: obj) (k: string) (v: obj) =
        capturedKey <- k
        o?(k) <- v

    let rewrite (o: obj) =
        o?("tag") <- "rewritten"
        o

    let outJson = createObj [ "jsonSchema", createObj [ "a", box 1 ] ]
    rewriteToolJsonSchema setKey rewrite outJson |> ignore
    equal "jsonSchema rewritten" "rewritten" (string (outJson?("jsonSchema")?("tag")))

let opencodeHookSchemaRewriteToolJsonSchemaParameters () =
    let mutable capturedKey = ""

    let setKey (o: obj) (k: string) (v: obj) =
        capturedKey <- k
        o?(k) <- v

    let rewrite (o: obj) =
        o?("tag") <- "rewritten"
        o

    let outParams = createObj [ "parameters", createObj [ "b", box 2 ] ]
    rewriteToolJsonSchema setKey rewrite outParams |> ignore
    equal "parameters rewritten" "rewritten" (string (outParams?("parameters")?("tag")))

let opencodeHookSchemaRewriteToolJsonSchemaNoSchema () =
    let mutable capturedKey = ""

    let setKey (o: obj) (k: string) (v: obj) =
        capturedKey <- k
        o?(k) <- v

    let rewrite (o: obj) =
        o?("tag") <- "rewritten"
        o

    let outNone = createObj []
    rewriteToolJsonSchema setKey rewrite outNone |> ignore
    equal "no crash key empty" "" capturedKey

let opencodeHookSchemaRewriteToolJsonSchemaArgsBranch () =
    let mutable capturedKey = ""

    let setKey (o: obj) (k: string) (v: obj) =
        capturedKey <- k
        o?(k) <- v

    let rewrite (o: obj) =
        o?("tag") <- "rewritten"
        o

    let outArgs = createObj [ "args", createObj [ "c", box 3 ] ]
    rewriteToolJsonSchema setKey rewrite outArgs |> ignore
    equal "args rewritten" "rewritten" (string (outArgs?("args")?("tag")))

// ── run ───────────────────────────────────────────────────────────────────────

let run () =
    promise {
        opencodeHookSchemaSetUiLabelCoder ()
        opencodeHookSchemaSetUiLabelInspector ()
        opencodeHookSchemaSetUiLabelOther ()
        opencodeHookSchemaStripUiFromJsonSchemaWithUi ()
        opencodeHookSchemaStripUiFromJsonSchemaNoUi ()
        opencodeHookSchemaStripUiFromJsonSchemaNull ()
        opencodeHookSchemaRewriteToolJsonSchemaJsonSchema ()
        opencodeHookSchemaRewriteToolJsonSchemaParameters ()
        opencodeHookSchemaRewriteToolJsonSchemaNoSchema ()
        opencodeHookSchemaRewriteToolJsonSchemaArgsBranch ()
    }
