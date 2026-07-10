module Wanxiangshu.Tests.KernelCoverageTestsMethodology

open Fable.Core
open Fable.Core.JsInterop
open System
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Methodology.Registry
open Wanxiangshu.Kernel.OmpPrompts
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Kernel.ReviewReplayPolicy
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Kernel.Messaging

// ── Kernel.Methodology ────────────────────────────────────────────────────────
let methToolResultText () =
    let r = methodologyToolResultText [ "first_principles" ]
    check "contains first_principles" (r.Contains "first_principles")

let methToolResultTextMulti () =
    let r = methodologyToolResultText [ "a"; "b" ]
    check "contains a" (r.Contains "a")
    check "contains b" (r.Contains "b")

let methTodoResultTextEmpty () =
    equal "empty todos" "Todos updated." (todoResultText [])

let methTodoResultTextOne () =
    let r = todoResultText [ "x" ]
    check "todo contains x" (r.Contains "x")

let methEnumCount () =
    check "enum count > 50" (enumValues.Value.Length > 50)

let methSelectFieldDesc () =
    check
        "selectMethodologyFieldDescription contains select_methodology"
        (selectMethodologyFieldDescription.Contains "select_methodology")

// ── Kernel.OmpPrompts ─────────────────────────────────────────────────────────
let ompEditorPrompt () =
    check "editorPrompt contains code editing" (editorPromptOmp.Contains "code editing")

let ompGreperPrompt () =
    check "greperPrompt contains fuzzy_find" (greperPromptOmp.Contains "fuzzy_find")

let ompBrowserPrompt () =
    check "browserPrompt contains browser" (browserPromptOmp.Contains "browser")

// ── Kernel.ToolArgs constructors ──────────────────────────────────────────────
let taRead () =
    let a =
        Read
            { Path = "f"
              Offset = None
              Limit = None }

    match a with
    | Read r -> equal "Read.Path" "f" r.Path
    | _ -> check "Read case" false

let taWrite () =
    let a = Write { FilePath = "f"; Content = "c" }

    match a with
    | Write w -> equal "Write.FilePath" "f" w.FilePath
    | _ -> check "Write case" false

let taBrowser () =
    let a = Browser { Intent = "browse" }

    match a with
    | Browser b -> equal "Browser.Intent" "browse" b.Intent
    | _ -> check "Browser case" false

let taWebsearch () =
    let a =
        Websearch
            { Query = "q"
              NumResults = 5
              WhatToSummarize = "s" }

    match a with
    | Websearch w -> equal "Websearch.Query" "q" w.Query
    | _ -> check "Websearch case" false

let taWebfetch () =
    let a =
        Webfetch
            { Url = "http://x"
              ExtractMain = None
              PreferLlmsTxt = None
              Prompt = None
              Timeout = None }

    match a with
    | Webfetch w -> equal "Webfetch.Url" "http://x" w.Url
    | _ -> check "Webfetch case" false

let taExecutor () =
    let a =
        Executor
            { Language = Shell
              Program = "p"
              Dependencies = []
              TimeoutType = Short
              Mode = "rw"
              WhatToSummarize = "s" }

    match a with
    | Executor e -> equal "Executor.Program" "p" e.Program
    | _ -> check "Executor case" false

let taTodoWrite () =
    let a =
        TodoWrite
            { AhaMoments = "am"
              ChangesAndReasons = "cr"
              Gotchas = "g"
              LessonsAndConventions = "lc"
              Plan = "p"
              Todos = [||]
              SelectMethodology = [] }

    match a with
    | TodoWrite t -> equal "TodoWrite.Plan" "p" t.Plan
    | _ -> check "TodoWrite case" false

let taApplyPatch () =
    let a = ApplyPatch { PatchText = "diff" }

    match a with
    | ApplyPatch p -> equal "ApplyPatch" "diff" p.PatchText
    | _ -> check "ApplyPatch case" false

let taSubmitReview () =
    let a =
        SubmitReview
            { Report = "r"
              AffectedFiles = [ "f" ] }

    match a with
    | SubmitReview s -> equal "SubmitReview.Report" "r" s.Report
    | _ -> check "SubmitReview case" false

// ── Kernel.ToolResult ─────────────────────────────────────────────────────────
let trWireEncodeResultOk () =
    equal "wireEncodeResult Ok" "done" (wireEncodeResult (Ok "done"))

let trWireEncodeResultError () =
    let text =
        wireEncodeResult (Error(Wanxiangshu.Kernel.Domain.ToolNotPermitted("a", "t")))

    check "error contains failed" (text.Contains "failed")
    check "error contains not permitted" (text.Contains "not permitted")

// ── Kernel.ReviewReplayPolicy ─────────────────────────────────────────────────
let rrpTextsFromFlatPartsTool () =
    let toolState =
        { status = "completed"
          output = "out"
          error = ""
          input = null
          operationAction = "" }

    let fp =
        { msgIndex = 0
          partIndex = 0
          isUser = false
          part = ToolPart("t", "c1", Some toolState, null) }

    let texts = textsFromFlatParts [ fp ] |> Seq.toList
    equal "tool output text" [ "out" ] texts

let rrpTextsFromFlatPartsText () =
    let fp =
        { msgIndex = 0
          partIndex = 0
          isUser = false
          part = TextPart "hello" }

    let texts = textsFromFlatParts [ fp ] |> Seq.toList
    equal "text part" [ "hello" ] texts

let rrpTextsFromFlatPartsOther () =
    let fp =
        { msgIndex = 0
          partIndex = 0
          isUser = false
          part = ToolPart("t", "c", None, null) }

    let texts = textsFromFlatParts [ fp ] |> Seq.toList
    equal "no output tool" [ "" ] texts

// ── Kernel.ReviewSession.Types ────────────────────────────────────────────────
let rstEmpty () =
    let e = Wanxiangshu.Kernel.ReviewSession.Types.empty "rs1" 100L
    equal "empty id" "rs1" e.id
    equal "empty state" ReviewState.Inactive e.state
    equal "empty version" 0 e.version
    equal "empty parentId" None e.parentId

let rstWithTask () =
    let e = Wanxiangshu.Kernel.ReviewSession.Types.empty "rs1" 100L
    let once = Wanxiangshu.Kernel.ReviewSession.Types.withTask "t1" e
    equal "withTask set" (Some "t1") once.originalTask
    equal "withTask version 1" 1 once.version
    let same = Wanxiangshu.Kernel.ReviewSession.Types.withTask "t1" once
    equal "withTask same version" once.version same.version
    let diff = Wanxiangshu.Kernel.ReviewSession.Types.withTask "t2" once
    equal "withTask diff version" 2 diff.version
    equal "withTask diff task" (Some "t2") diff.originalTask

let rstWithFeedback () =
    let e = Wanxiangshu.Kernel.ReviewSession.Types.empty "rs1" 100L
    let fb = Wanxiangshu.Kernel.ReviewSession.Types.withFeedback e "good"
    equal "feedback set" (Some "good") fb.lastFeedback
    let same = Wanxiangshu.Kernel.ReviewSession.Types.withFeedback fb "good"
    equal "feedback same version" fb.version same.version
    let diff = Wanxiangshu.Kernel.ReviewSession.Types.withFeedback fb "bad"
    equal "feedback new version" (fb.version + 1) diff.version
    equal "feedback new text" (Some "bad") diff.lastFeedback

let rstAddChild () =
    let e = Wanxiangshu.Kernel.ReviewSession.Types.empty "rs1" 100L
    let c1 = Wanxiangshu.Kernel.ReviewSession.Types.addChild e "c1"
    equal "addChild new" [ "c1" ] c1.childIds
    equal "addChild version" (e.version + 1) c1.version
    let dup = Wanxiangshu.Kernel.ReviewSession.Types.addChild c1 "c1"
    equal "addChild dup" [ "c1" ] dup.childIds
    equal "addChild dup version same" c1.version dup.version

// ── Kernel.Config ─────────────────────────────────────────────────────────────
let cfgStealthBrowserRef () =
    equal "empty → master" "master" (stealthBrowserMcpRef "")
    equal "non-empty passthrough" "feat" (stealthBrowserMcpRef "feat")

let cfgStealthBrowserCommand () =
    let cmd = getStealthBrowserMcpCommand ""
    check "cmd has uvx" (cmd.Contains "uvx")
    check "cmd has 3.13" (cmd.Contains "3.13")
    check "cmd has repo" (cmd.Contains "github.com/vibheksoni/stealth-browser-mcp")

let cfgStealthBrowserLocalConfig () =
    let cfg = getStealthBrowserMcpLocalConfig ""
    equal "localConfig type" "local" cfg.``type``
    let cmd = cfg.command
    check "cmd has uvx" (Array.contains "uvx" cmd)
    check "cmd has python" (Array.contains "python" cmd)

let run () =
    methToolResultText ()
    methToolResultTextMulti ()
    methTodoResultTextEmpty ()
    methTodoResultTextOne ()
    methEnumCount ()
    methSelectFieldDesc ()
    ompEditorPrompt ()
    ompGreperPrompt ()
    ompBrowserPrompt ()
    taRead ()
    taWrite ()
    taBrowser ()
    taWebsearch ()
    taWebfetch ()
    taExecutor ()
    taTodoWrite ()
    taApplyPatch ()
    taSubmitReview ()
    trWireEncodeResultOk ()
    trWireEncodeResultError ()
    rrpTextsFromFlatPartsTool ()
    rrpTextsFromFlatPartsText ()
    rrpTextsFromFlatPartsOther ()
    rstEmpty ()
    rstWithTask ()
    rstWithFeedback ()
    rstAddChild ()
    cfgStealthBrowserRef ()
    cfgStealthBrowserCommand ()
    cfgStealthBrowserLocalConfig ()
