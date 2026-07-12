module Wanxiangshu.Tests.OpencodeHookSchemaCoverageTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Opencode.HookSchema
open Wanxiangshu.Shell.WorkBacklogSchema
open Wanxiangshu.Kernel.WarnTdd
open Wanxiangshu.Shell.Dyn

module Dyn = Wanxiangshu.Shell.Dyn

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
    check "coder _ui set" (not (Dyn.isNullish (Dyn.get args "_ui")))

let opencodeHookSchemaSetUiLabelInvestigator () =
    let args =
        createObj
            [ "intents",
              box
                  [| box
                         {| objective = "Investigate"
                            background = "reason"
                            questions = [| "Q1" |]
                            entries = [||] |} |] ]

    setUiLabel args "investigator"
    check "investigator _ui set" (not (Dyn.isNullish (Dyn.get args "_ui")))

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
    check "other _ui not set" (Dyn.isNullish (Dyn.get args "_ui"))

// ── stripUiFromJsonSchema ─────────────────────────────────────────────────────

let opencodeHookSchemaStripUiFromJsonSchemaWithUi () =
    let schema =
        createObj
            [ "type", box "object"
              "properties", createObj [ "name", box (createObj []); "_ui", box (createObj []) ] ]

    let result = stripUiFromJsonSchema schema
    check "type preserved" (Dyn.str result "type" = "object")
    check "_ui removed" (Dyn.isNullish (Dyn.get (Dyn.get result "properties") "_ui"))
    check "name kept" (not (Dyn.isNullish (Dyn.get (Dyn.get result "properties") "name")))

let opencodeHookSchemaStripUiFromJsonSchemaNoUi () =
    let schema =
        createObj [ "type", box "object"; "properties", createObj [ "name", box (createObj []) ] ]

    let result = stripUiFromJsonSchema schema
    check "type preserved no _ui" (Dyn.str result "type" = "object")
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

// ── injectWarnTddIntoJsonSchema ───────────────────────────────────────────────

let opencodeHookSchemaInjectWarnTddIntoEmptySchema () =
    let schema =
        createObj [ "type", box "object"; "properties", createObj [ "name", box (createObj []) ] ]

    injectWarnTddIntoJsonSchema schema |> ignore
    let props = get schema "properties"
    check "warn_tdd property injected" (not (Dyn.isNullish (get props "warn_tdd")))
    let prop = get props "warn_tdd"
    check "warn_tdd has soft-required metadata" (Dyn.truthy (get prop "x-wanxiangshu-soft-required"))
    let required = get schema "required"

    check
        "warn_tdd NOT added to required"
        (Dyn.isNullish required
         || not (required :?> obj array |> Array.exists (fun x -> string x = "warn_tdd")))

let opencodeHookSchemaInjectWarnTddAlreadyPresent () =
    let schema =
        createObj
            [ "type", box "object"
              "properties", createObj [ "warn_tdd", box (createObj []) ]
              "required", box [| box "warn_tdd" |] ]

    injectWarnTddIntoJsonSchema schema |> ignore
    let props = get schema "properties"
    check "existing warn_tdd still present" (not (Dyn.isNullish (get props "warn_tdd")))

let opencodeHookSchemaInjectWarnTddNullSchema () =
    let result = injectWarnTddIntoJsonSchema null
    check "null schema returns null" (isNull result)

// ── mergeWorkBacklogReportIntoTaskSchema ─────────────────────────────────────

let opencodeHookSchemaMergeWorkBacklogReportIntoPureSchema () =
    let schema =
        createObj [ "type", box "object"; "properties", createObj [ "name", box (createObj []) ] ]

    let result = mergeWorkBacklogReportIntoTaskSchema schema
    let props = get result "properties"
    check "ahaMoments added" (not (Dyn.isNullish (get props "ahaMoments")))
    check "select_methodology added" (not (Dyn.isNullish (get props "select_methodology")))

let opencodeHookSchemaMergeWorkBacklogReportRemoveTaskId () =
    let schema =
        createObj
            [ "type", box "object"
              "properties",
              createObj
                  [ "task_id", box (createObj [ "type", box "string" ])
                    "description", box (createObj [ "type", box "string" ]) ]
              "required", box [| box "task_id"; box "description" |] ]

    let result = mergeWorkBacklogReportIntoTaskSchema schema
    let resultProps = get result "properties"
    let resultRequired = get result "required"
    let props = resultProps
    check "task_id removed from properties" (Dyn.isNullish (get props "task_id"))
    check "ahaMoments added" (not (Dyn.isNullish (get props "ahaMoments")))
    check "select_methodology added" (not (Dyn.isNullish (get props "select_methodology")))
    let required = resultRequired

    check
        "task_id absent from required"
        (not (isArray required)
         || not ((required :?> obj array) |> Array.exists (fun x -> string x = "task_id")))

let opencodeHookSchemaMergeWorkBacklogReportSoftenExistingFields () =
    let schema =
        createObj
            [ "type", box "object"
              "properties",
              createObj
                  [ "plan",
                    createObj
                        [ "type", box "string"
                          "minLength", box 1024
                          "description", box "Original plan description" ]
                    "description", createObj [ "type", box "string" ] ]
              "required", box [| box "plan"; box "description" |] ]

    let result = mergeWorkBacklogReportIntoTaskSchema schema
    let resultProps = get result "properties"
    let resultRequired = get result "required"
    let planProp = get resultProps "plan"

    check "minLength removed from plan" (Dyn.isNullish (get planProp "minLength"))
    check "soft min length added to plan" (unbox<int> (get planProp "x-wanxiangshu-soft-min-length") = 1024)
    check "plan description softened" ((Dyn.str planProp "description").Contains("MUST be at least 1024 characters"))

    let required = resultRequired :?> obj array
    check "plan removed from required" (not (required |> Array.exists (fun x -> string x = "plan")))
    check "description still in required" (required |> Array.exists (fun x -> string x = "description"))

// ── buildWorkBacklogSchema ────────────────────────────────────────────────────

let opencodeHookSchemaTryBuildJsonSchemaFromEffectSchemaDefs () =
    let effectStruct (shape: obj) : obj =
        callMethod1 effectSchemaNs "Struct" shape

    let effectString: obj = get effectSchemaNs "String"

    let structInstance = effectStruct (createObj [ "question", effectString ])

    let promptSchema =
        callMethod1 structInstance "annotate" (createObj [ "identifier", box "QuestionPrompt" ])

    let arrayType = callMethod1 effectSchemaNs "Array" promptSchema
    let parentSchema = effectStruct (createObj [ "questions", arrayType ])

    let schema =
        Wanxiangshu.Opencode.HookSchemaCore.tryBuildJsonSchemaFromEffectSchema parentSchema

    check "schema built successfully" (not (isNullish schema))

    let defs = get schema "$defs"
    check "$defs is present in schema" (not (isNullish defs))

    let questionPrompt = get defs "QuestionPrompt"
    check "QuestionPrompt definition is present in $defs" (not (isNullish questionPrompt))

let opencodeHookSchemaBuildWorkBacklogSchema () =
    let schema = buildWorkBacklogSchema ()
    check "schema is non-null" (not (isNull schema))
    let typeVal = Dyn.str schema "type"
    check "schema type = object" (typeVal = "object")
    let props = get schema "properties"
    check "properties present" (not (Dyn.isNullish props))
    let todos = Dyn.get props "todos"
    check "todos field present" (not (Dyn.isNullish todos))
    let items = Dyn.get (Dyn.get todos "items") "properties"
    check "todo item properties present" (not (Dyn.isNullish items))

// ── run ───────────────────────────────────────────────────────────────────────

let run () =
    promise {
        opencodeHookSchemaSetUiLabelCoder ()
        opencodeHookSchemaSetUiLabelInvestigator ()
        opencodeHookSchemaSetUiLabelOther ()
        opencodeHookSchemaStripUiFromJsonSchemaWithUi ()
        opencodeHookSchemaStripUiFromJsonSchemaNoUi ()
        opencodeHookSchemaStripUiFromJsonSchemaNull ()
        opencodeHookSchemaInjectWarnTddIntoEmptySchema ()
        opencodeHookSchemaBuildWorkBacklogSchema ()
        opencodeHookSchemaRewriteToolJsonSchemaJsonSchema ()
        opencodeHookSchemaRewriteToolJsonSchemaParameters ()
        opencodeHookSchemaRewriteToolJsonSchemaNoSchema ()
        opencodeHookSchemaRewriteToolJsonSchemaArgsBranch ()
        opencodeHookSchemaInjectWarnTddAlreadyPresent ()
        opencodeHookSchemaInjectWarnTddNullSchema ()
        opencodeHookSchemaMergeWorkBacklogReportIntoPureSchema ()
        opencodeHookSchemaMergeWorkBacklogReportRemoveTaskId ()
        opencodeHookSchemaMergeWorkBacklogReportSoftenExistingFields ()
        opencodeHookSchemaTryBuildJsonSchemaFromEffectSchemaDefs ()
    }
