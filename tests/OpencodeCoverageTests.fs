module Wanxiangshu.Tests.OpencodeCoverageTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Opencode.HookSchema
open Wanxiangshu.Opencode.SearchTools
open Wanxiangshu.Opencode.SessionIoSubagent
open Wanxiangshu.Opencode.MessagingCodec
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FuzzyIteratorStore
open Wanxiangshu.Shell.FuzzyFinderShell
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.WebToolsCodec
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.ToolCatalog

module Dyn = Wanxiangshu.Shell.Dyn

// ── HookSchema.setUiLabel ─────────────────────────────────────────────────────

let hookSchemaSetUiLabelCoder () =
    let args = createObj [ "intents", box [| box (createObj [
        "objective", box "Fix bug"
        "background", box "reason"
        "targets", box [| createObj [ "file", box "src/a.fs"; "guide", box "fix it" ] |]
        "do_not_touch", box [||]
    ]) |] ]
    setUiLabel args "coder"
    check "coder _ui set" (not (Dyn.isNullish (Dyn.get args "_ui")))

let hookSchemaSetUiLabelInvestigator () =
    let args = createObj [ "intents", box [| box {| objective = "Investigate"; background = "reason"; questions = [| "Q1" |]; entries = [||] |} |] ]
    setUiLabel args "investigator"
    check "investigator _ui set" (not (Dyn.isNullish (Dyn.get args "_ui")))

let hookSchemaSetUiLabelOther () =
    let args = createObj [ "intents", box [| box {| objective = "Fix bug"; background = "reason"; targets = [||]; do_not_touch = [||] |} |] ]
    setUiLabel args "other"
    check "other _ui not set" (Dyn.isNullish (Dyn.get args "_ui"))

// ── HookSchema.stripUiFromJsonSchema ─────────────────────────────────────────

let hookSchemaStripUiFromJsonSchemaWithUi () =
    let schema = createObj [ "type", box "object"; "properties", createObj [ "name", box (createObj []); "_ui", box (createObj []) ] ]
    let result = stripUiFromJsonSchema schema
    check "type preserved" (Dyn.str result "type" = "object")
    check "_ui removed" (Dyn.isNullish (Dyn.get (Dyn.get result "properties") "_ui"))
    check "name kept" (not (Dyn.isNullish (Dyn.get (Dyn.get result "properties") "name")))

let hookSchemaStripUiFromJsonSchemaNoUi () =
    let schema = createObj [ "type", box "object"; "properties", createObj [ "name", box (createObj []) ] ]
    let result = stripUiFromJsonSchema schema
    check "type preserved no _ui" (Dyn.str result "type" = "object")
    check "name still present" (not (Dyn.isNullish (Dyn.get (Dyn.get result "properties") "name")))

let hookSchemaStripUiFromJsonSchemaNull () =
    let result = stripUiFromJsonSchema null
    check "null returns null" (isNull result)


let hookSchemaRewriteToolJsonSchemaJsonSchema () =
    let mutable capturedKey = ""
    let setKey (o: obj) (k: string) (v: obj) = capturedKey <- k; o?(k) <- v
    let rewrite (o: obj) = o?("tag") <- "rewritten"; o
    let outJson = createObj [ "jsonSchema", createObj [ "a", box 1 ] ]
    rewriteToolJsonSchema setKey rewrite outJson |> ignore
    equal "jsonSchema rewritten" "rewritten" (string (outJson?("jsonSchema")?("tag")))

let hookSchemaRewriteToolJsonSchemaParameters () =
    let mutable capturedKey = ""
    let setKey (o: obj) (k: string) (v: obj) = capturedKey <- k; o?(k) <- v
    let rewrite (o: obj) = o?("tag") <- "rewritten"; o
    let outParams = createObj [ "parameters", createObj [ "b", box 2 ] ]
    rewriteToolJsonSchema setKey rewrite outParams |> ignore
    equal "parameters rewritten" "rewritten" (string (outParams?("parameters")?("tag")))

let hookSchemaRewriteToolJsonSchemaNoSchema () =
    let mutable capturedKey = ""
    let setKey (o: obj) (k: string) (v: obj) = capturedKey <- k; o?(k) <- v
    let rewrite (o: obj) = o?("tag") <- "rewritten"; o
    let outNone = createObj []
    rewriteToolJsonSchema setKey rewrite outNone |> ignore
    equal "no crash key empty" "" capturedKey

let hookSchemaRewriteToolJsonSchemaArgsBranch () =
    let mutable capturedKey = ""
    let setKey (o: obj) (k: string) (v: obj) = capturedKey <- k; o?(k) <- v
    let rewrite (o: obj) = o?("tag") <- "rewritten"; o
    let outArgs = createObj [ "args", createObj [ "c", box 3 ] ]
    rewriteToolJsonSchema setKey rewrite outArgs |> ignore
    equal "args rewritten" "rewritten" (string (outArgs?("args")?("tag")))


let searchToolsFuzzyFindTool () =
    let finderCache = FinderCache()
    let iteratorStore = createTypedIteratorStore 200
    let tool = fuzzyFindTool finderCache iteratorStore
    check "fuzzyFind tool non-null" (not (isNull tool))

let searchToolsFuzzyGrepTool () =
    let finderCache = FinderCache()
    let iteratorStore = createTypedIteratorStore 200
    let tool = fuzzyGrepTool finderCache iteratorStore
    check "fuzzyGrep tool non-null" (not (isNull tool))

let searchToolsWebsearchTool () =
    let registry = ChildAgentRegistry.Create()
    let ctx = createObj []
    let tool = websearchTool Opencode registry ctx (Wanxiangshu.Shell.FallbackRuntimeState.FallbackRuntimeState())
    check "websearch tool non-null" (not (isNull tool))

let searchToolsWebfetchTool () =
    let ctx = createObj []
    let tool = webfetchTool ctx
    check "webfetch tool non-null" (not (isNull tool))


let searchToolsFuzzyFindToolName () =
    let finderCache = FinderCache()
    let iteratorStore = createTypedIteratorStore 200
    let tool = fuzzyFindTool finderCache iteratorStore
    let spec = specOf "fuzzy_find"
    equal "fuzzyFind tool name" spec.name "fuzzy_find"

let searchToolsFuzzyGrepToolName () =
    let finderCache = FinderCache()
    let iteratorStore = createTypedIteratorStore 200
    let tool = fuzzyGrepTool finderCache iteratorStore
    let spec = specOf "fuzzy_grep"
    equal "fuzzyGrep tool name" spec.name "fuzzy_grep"

let searchToolsWebfetchToolName () =
    let ctx = createObj []
    let tool = webfetchTool ctx
    let spec = specOf "webfetch"
    equal "webfetch tool name" spec.name "webfetch"

// ── SearchTools webfetch full options decode ──────────────────────────────────

let searchToolsWebfetchToolFullOptionsDecode () =
    let args = createObj [
        "url", box "https://example.com"
        "extract_main", box true
        "prefer_llms_txt", box "auto"
        "prompt", box "summarize"
        "timeout", box 30
    ]
    match decodeWebfetchArgs args with
    | Error e -> check "webfetch decode should succeed" false
    | Ok wf ->
        check "url decoded" (wf.Url = "https://example.com")
        check "extract_main decoded" (wf.ExtractMain = Some true)
        check "prefer_llms_txt decoded" (wf.PreferLlmsTxt = Some "auto")
        check "prompt decoded" (wf.Prompt = Some "summarize")
        check "timeout decoded" (wf.Timeout = Some 30)

// ── SessionIoSubagent ─────────────────────────────────────────────────────────

let sessionIoSubagentBuildPromptBodyMinimal () =
    let options = { agent = "executor"; title = "T"; prompt = "do it"; directory = "/tmp"; sessionID = ""; tools = null; aiSettings = { modelString = None; thinkingLevel = None } }
    let body = buildPromptBody options "child-1"
    check "path.id present" (not (Dyn.isNullish (Dyn.get (Dyn.get body "path") "id")))
    check "body.agent present" (Dyn.str (Dyn.get body "body") "agent" = "executor")

let sessionIoSubagentBuildPromptBodyTools () =
    let toolsObj = createObj []
    let options = { agent = "executor"; title = "T"; prompt = "do it"; directory = "/tmp"; sessionID = ""; tools = toolsObj; aiSettings = { modelString = None; thinkingLevel = None } }
    let body = buildPromptBody options "child-2"
    check "body.tools present" (not (Dyn.isNullish (Dyn.get (Dyn.get body "body") "tools")))

let sessionIoSubagentBuildPromptBodyModel () =
    let options = { agent = "executor"; title = "T"; prompt = "do it"; directory = "/tmp"; sessionID = ""; tools = null; aiSettings = { modelString = Some "openai/gpt-4"; thinkingLevel = None } }
    let body = buildPromptBody options "child-3"
    check "body.model present" (not (Dyn.isNullish (Dyn.get (Dyn.get body "body") "model")))

let sessionIoSubagentBuildPromptBodyThinkingLevel () =
    let options = { agent = "executor"; title = "T"; prompt = "do it"; directory = "/tmp"; sessionID = ""; tools = null; aiSettings = { modelString = None; thinkingLevel = Some "high" } }
    let body = buildPromptBody options "child-4"
    check "body.variant present" (not (Dyn.isNullish (Dyn.get (Dyn.get body "body") "variant")))

let sessionIoSubagentInvoke1 () =
    let mutable receivedArg = ""
    let target = createObj [ "myMethod", box (fun (arg: obj) -> receivedArg <- string arg; box "result") ]
    let p = invoke1 (box "hello") "myMethod" target
    equal "invoke1 result" "result" (string (unbox p))
    equal "invoke1 arg passed" "hello" receivedArg

let sessionIoSubagentExtractSessionText () =
    let fakeData =
        [| createObj [
            "info", box (createObj [ "id", box "msg-1"; "sessionID", box "sess-1"; "role", box "assistant"; "agent", box "a"; "isError", box false; "toolName", box ""; "details", box null; "time", box null ])
            "parts", box [| createObj [ "type", box "text"; "text", box "Hello from assistant" ] |]
        ] |]
    let fakeSession = createObj [ "messages", box (fun (_arg: obj) -> Promise.lift (box (createObj [ "data", box fakeData ]))) ]
    let fakeClient = createObj [ "session", box fakeSession ]
    promise {
        let! text = extractSessionText fakeClient "sess-1" ""
        equal "assistant text" "Hello from assistant" text
    }

open Wanxiangshu.Kernel.WarnTdd


let hookSchemaInjectWarnTddIntoEmptySchema () =
    let schema = createObj [ "type", box "object"; "properties", createObj [ "name", box (createObj []) ] ]
    injectWarnTddIntoJsonSchema schema |> ignore
    let props = get schema "properties"
    check "warn_tdd property injected" (not (Dyn.isNullish (get props "warn_tdd")))
    let required = get schema "required"
    check "warn_tdd added to required" (isArray required && (required :?> obj array |> Array.exists (fun x -> string x = "warn_tdd")))

let hookSchemaInjectWarnTddAlreadyPresent () =
    let schema = createObj [ "type", box "object"; "properties", createObj [ "warn_tdd", box (createObj []) ]; "required", box [| box "warn_tdd" |] ]
    injectWarnTddIntoJsonSchema schema |> ignore
    let props = get schema "properties"
    // Should not throw and should keep the existing warn_tdd (no double write)
    check "existing warn_tdd still present" (not (Dyn.isNullish (get props "warn_tdd")))

let hookSchemaInjectWarnTddNullSchema () =
    let result = injectWarnTddIntoJsonSchema null
    check "null schema returns null" (isNull result)


let hookSchemaMergeWorkBacklogReportIntoPureSchema () =
    let schema = createObj [ "type", box "object"; "properties", createObj [ "name", box (createObj []) ] ]
    let result = mergeWorkBacklogReportIntoTaskSchema schema
    let props = get result "properties"
    check "completedWorkReport added" (not (Dyn.isNullish (get props "completedWorkReport")))
    check "select_methodology added" (not (Dyn.isNullish (get props "select_methodology")))

let hookSchemaMergeWorkBacklogReportRemoveTaskId () =
    let schema =
        createObj [
            "type", box "object"
            "properties", createObj [
                "task_id", box (createObj [ "type", box "string" ])
                "description", box (createObj [ "type", box "string" ])
            ]
            "required", box [| box "task_id"; box "description" |]
        ]
    // ── 调用 SUT ──
    let result = mergeWorkBacklogReportIntoTaskSchema schema
    let resultProps = get result "properties"
    let resultRequired = get result "required"
    let props = resultProps
    check "task_id removed from properties" (Dyn.isNullish (get props "task_id"))
    check "completedWorkReport added" (not (Dyn.isNullish (get props "completedWorkReport")))
    check "select_methodology added" (not (Dyn.isNullish (get props "select_methodology")))
    let required = resultRequired
    check "task_id absent from required" (not (isArray required) || not ((required :?> obj array) |> Array.exists (fun x -> string x = "task_id")))

// ── HookSchema.buildWorkBacklogSchema ──────────────────────────────────────────

let hookSchemaBuildWorkBacklogSchema () =
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

let run () = promise {
    hookSchemaSetUiLabelCoder ()
    hookSchemaSetUiLabelInvestigator ()
    hookSchemaSetUiLabelOther ()
    hookSchemaStripUiFromJsonSchemaWithUi ()
    hookSchemaStripUiFromJsonSchemaNoUi ()
    hookSchemaStripUiFromJsonSchemaNull ()
    hookSchemaInjectWarnTddIntoEmptySchema ()
    hookSchemaBuildWorkBacklogSchema ()
    hookSchemaRewriteToolJsonSchemaJsonSchema ()
    hookSchemaRewriteToolJsonSchemaParameters ()
    hookSchemaRewriteToolJsonSchemaNoSchema ()
    hookSchemaRewriteToolJsonSchemaArgsBranch ()
    hookSchemaInjectWarnTddAlreadyPresent ()
    hookSchemaInjectWarnTddNullSchema ()
    hookSchemaMergeWorkBacklogReportIntoPureSchema ()
    hookSchemaMergeWorkBacklogReportRemoveTaskId ()
    searchToolsFuzzyFindTool ()
    searchToolsFuzzyGrepTool ()
    searchToolsWebsearchTool ()
    searchToolsWebfetchTool ()
    searchToolsWebfetchToolFullOptionsDecode ()
    searchToolsFuzzyFindToolName ()
    searchToolsFuzzyGrepToolName ()
    searchToolsWebfetchToolName ()
    sessionIoSubagentBuildPromptBodyMinimal ()
    sessionIoSubagentBuildPromptBodyTools ()
    sessionIoSubagentBuildPromptBodyModel ()
    sessionIoSubagentBuildPromptBodyThinkingLevel ()
    sessionIoSubagentInvoke1 ()
    do! sessionIoSubagentExtractSessionText ()
}
