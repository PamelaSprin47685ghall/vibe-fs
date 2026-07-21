module Wanxiangshu.Tests.KernelDomainCoverageTestsToolArgs

open Fable.Core
open Fable.Core.JsInterop
open System
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Kernel.Executor
open Wanxiangshu.Runtime.ExecutorFormat
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
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
              Command = "p"
              Dependencies = []
              TimeoutType = Short
              WhatToSummarize = "s" }

    match a with
    | Executor e -> equal "Executor.Command" "p" e.Command
    | _ -> check "Executor case" false

let taTodoWrite () =
    let a = TodoWrite { Todos = [||]; SelectMethodology = [] }

    match a with
    | TodoWrite t -> equal "TodoWrite.Todos" 0 t.Todos.Length
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
    let text = wireEncodeResult (Error(ToolNotPermitted("a", "t")))
    check "error contains failed" (text.Contains "failed")
    check "error contains not permitted" (text.Contains "not permitted")

let run () =
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
