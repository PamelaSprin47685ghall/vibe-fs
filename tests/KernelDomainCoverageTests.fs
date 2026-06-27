module Wanxiangshu.Tests.KernelDomainCoverageTests

open Fable.Core
open Fable.Core.JsInterop
open System
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.Domain.Id
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Kernel.OmpPrompts
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Kernel.Executor

// ── Kernel.Domain.Id ─────────────────────────────────────────────────────────

let idRoundTrip parser value label extract input =
    match parser input with
    | Ok v -> equal (value + " " + label) input (extract v)
    | Error _ -> check (value + " parse") false

let idError parser input =
    check (input + " error") (Result.isError (parser input))

let idSessionIdRoundtrip () =
    let sid = sessionId "s1"
    equal "sessionId Some" true (Result.isOk sid)
    match sid with Ok v -> equal "sessionIdValue" "s1" (sessionIdValue v) | _ -> check "sessionId ok" false

let idWorkspaceIdRoundtrip () =
    idRoundTrip workspaceId "workspaceId" "" workspaceIdValue "w1"

let idAgentIdRoundtrip () =
    idRoundTrip agentId "agentId" "" agentIdValue "a1"

let idToolIdRoundtrip () =
    idRoundTrip toolId "toolId" "" toolIdValue "t1"

let idCallIdRoundtrip () =
    idRoundTrip callId "callId" "" callIdValue "c1"

let idChildIdRoundtrip () =
    idRoundTrip childId "childId" "" childIdValue "ch1"

let idQuick () =
    let w = workspaceIdQuick "wq"
    equal "workspaceIdQuick" "wq" (workspaceIdValue w)
    let a = agentIdQuick "aq"
    equal "agentIdQuick" "aq" (agentIdValue a)

let idTryValid () =
    equal "trySessionId ok" (Some "s1") (trySessionId "s1" |> Option.map sessionIdValue)
    equal "tryWorkspaceId ok" (Some "w1") (tryWorkspaceId "w1" |> Option.map workspaceIdValue)
    equal "tryAgentId ok" (Some "a1") (tryAgentId "a1" |> Option.map agentIdValue)

let idTryEmpty () =
    equal "trySessionId empty" None (trySessionId "")
    equal "tryWorkspaceId empty" None (tryWorkspaceId "")
    equal "tryAgentId empty" None (tryAgentId "")

// ── Kernel.Domain.Error ───────────────────────────────────────────────────────
let formatAllErrors () =
    equal "MessageAborted" "aborted" (formatDomainError MessageAborted)
    equal "SessionBusy" "session busy" (formatDomainError SessionBusy)
    equal "TaskWaitBackgrounded" "task wait backgrounded" (formatDomainError TaskWaitBackgrounded)
    equal "ExecutorExecutableMissing" "executable not found: x" (formatDomainError (ExecutorExecutableMissing "x"))
    equal "ParseError" "parse error in ctx: d" (formatDomainError (ParseError(context = "ctx", detail = "d")))
    equal "ToolNotPermitted" "tool 't' not permitted for agent 'a'" (formatDomainError (ToolNotPermitted("a", "t")))
    equal "InvalidIntent" "invalid f for tool 't': d" (formatDomainError (InvalidIntent("t", "f", "d")))
    equal "UpstreamTimeout" "upstream timeout after 5s" (formatDomainError (UpstreamTimeout 5))
    equal "UpstreamRefused" "upstream refused: r" (formatDomainError (UpstreamRefused "r"))
    equal "SystemPanic" "system panic: p" (formatDomainError (SystemPanic "p"))
    equal "UnknownJsError" "u" (formatDomainError (UnknownJsError "u"))

let isAbortChecks () =
    check "MessageAborted is abort" (isAbort MessageAborted)
    check "SessionBusy not abort" (not (isAbort SessionBusy))
    check "TaskWaitBackgrounded not abort" (not (isAbort TaskWaitBackgrounded))
    check "ExecutorExecutableMissing not abort" (not (isAbort (ExecutorExecutableMissing "x")))
    check "ParseError not abort" (not (isAbort (ParseError("c", "d"))))
    check "ToolNotPermitted not abort" (not (isAbort (ToolNotPermitted("a", "t"))))
    check "InvalidIntent not abort" (not (isAbort (InvalidIntent("t", "f", "d"))))
    check "UpstreamTimeout 5 not abort" (not (isAbort (UpstreamTimeout 5)))
    check "UpstreamRefused r not abort" (not (isAbort (UpstreamRefused "r")))
    check "SystemPanic p not abort" (not (isAbort (SystemPanic "p")))
    check "UnknownJsError u not abort" (not (isAbort (UnknownJsError "u")))

let containsAbortChecks () =
    check "abort lower" (containsAbortText "please abort now")
    check "empty false" (not (containsAbortText ""))
    check "normal false" (not (containsAbortText "normal"))
    check "Abort capital" (containsAbortText "Abort")

let classifyErrorLeafChecks () =
    equal "AbortError name" MessageAborted (classifyErrorLeaf "AbortError" "" "msg")
    equal "MessageAborted tag" MessageAborted (classifyErrorLeaf "" "MessageAborted" "msg")
    equal "SessionBusyError name" SessionBusy (classifyErrorLeaf "SessionBusyError" "" "msg")
    equal "TaskWaitBackgrounded tag" TaskWaitBackgrounded (classifyErrorLeaf "" "TaskWaitBackgrounded" "msg")
    equal "Other+abort text" MessageAborted (classifyErrorLeaf "Other" "Other" "abort text")
    equal "Other no abort" (UnknownJsError "normal") (classifyErrorLeaf "Other" "Other" "normal")

// ── Kernel.Domain.WorkspaceState ──────────────────────────────────────────────
let workspaceStateEmpty () =
    check "empty childSessions empty" (Map.isEmpty empty.childSessions)

let workspaceStateReduceRegister () =
    let cid = match childId "ch1" with Ok v -> v | Error _ -> failwith "childId failed"
    let ev = ChildRegistered(cid, { agent = "a"; parentSessionId = None })
    let s = reduce empty ev
    equal "reduce count 1" 1 (Map.count s.childSessions)

let workspaceStateReduceUnregister () =
    let cid = match childId "ch1" with Ok v -> v | Error _ -> failwith "childId failed"
    let evReg = ChildRegistered(cid, { agent = "a"; parentSessionId = None })
    let s = reduce empty evReg
    let evUnreg = ChildUnregistered cid
    let s2 = reduce s evUnreg
    check "reduce unregister back to empty" (Map.isEmpty s2.childSessions)

// ── Kernel.Methodology ────────────────────────────────────────────────────────
let methToolResultText () =
    let r = methodologyToolResultText ["first_principles"]
    check "contains first_principles" (r.Contains "first_principles")

let methToolResultTextMulti () =
    let r = methodologyToolResultText ["a"; "b"]
    check "contains a" (r.Contains "a")
    check "contains b" (r.Contains "b")

let methTodoResultTextEmpty () =
    equal "empty todos" "Todos updated." (todoResultText [])

let methTodoResultTextOne () =
    let r = todoResultText ["x"]
    check "todo contains x" (r.Contains "x")

let methEnumCount () =
    check "enum count > 50" (methodologyEnumValues.Length > 50)

let methSelectFieldDesc () =
    check "selectMethodologyFieldDescription contains select_methodology"
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
    let a = Read { Path = "f"; Offset = None; Limit = None }
    match a with Read r -> equal "Read.Path" "f" r.Path | _ -> check "Read case" false

let taWrite () =
    let a = Write { FilePath = "f"; Content = "c" }
    match a with Write w -> equal "Write.FilePath" "f" w.FilePath | _ -> check "Write case" false

let taMeditator () =
    let a = Meditator { Intent = "i"; Files = [||] }
    match a with Meditator m -> equal "Meditator.Intent" "i" m.Intent | _ -> check "Meditator case" false

let taBrowser () =
    let a = Browser { Intent = "browse" }
    match a with Browser b -> equal "Browser.Intent" "browse" b.Intent | _ -> check "Browser case" false

let taWebsearch () =
    let a = Websearch { Query = "q"; NumResults = 5; WhatToSummarize = "s" }
    match a with Websearch w -> equal "Websearch.Query" "q" w.Query | _ -> check "Websearch case" false

let taWebfetch () =
    let a = Webfetch { Url = "http://x"; ExtractMain = None; PreferLlmsTxt = None; Prompt = None; Timeout = None }
    match a with Webfetch w -> equal "Webfetch.Url" "http://x" w.Url | _ -> check "Webfetch case" false

let taExecutor () =
    let a = Executor { Language = Shell; Program = "p"; Dependencies = []; TimeoutType = Short; Mode = "rw" }
    match a with Executor e -> equal "Executor.Program" "p" e.Program | _ -> check "Executor case" false

let taTodoWrite () =
    let a = TodoWrite { CompletedWorkReport = "r"; Todos = [||]; SelectMethodology = [] }
    match a with TodoWrite t -> equal "TodoWrite.Report" "r" t.CompletedWorkReport | _ -> check "TodoWrite case" false

let taKnowledgeGraphFetch () =
    let a = KnowledgeGraphFetch { Entity = "kg1" }
    match a with KnowledgeGraphFetch k -> equal "KG.Entity" "kg1" k.Entity | _ -> check "KG case" false

let taReturnBookkeeper () =
    let a = ReturnBookkeeper []
    match a with ReturnBookkeeper _ -> check "ReturnBookkeeper case" true | _ -> check "ReturnBookkeeper case" false

let taApplyPatch () =
    let a = ApplyPatch { PatchText = "diff" }
    match a with ApplyPatch p -> equal "ApplyPatch" "diff" p.PatchText | _ -> check "ApplyPatch case" false

let taSubmitReview () =
    let a = SubmitReview { Report = "r"; AffectedFiles = ["f"] }
    match a with SubmitReview s -> equal "SubmitReview.Report" "r" s.Report | _ -> check "SubmitReview case" false

// ── Kernel.ToolResult ─────────────────────────────────────────────────────────
let trWireEncodeResultOk () =
    equal "wireEncodeResult Ok" "done" (wireEncodeResult (Ok "done"))

let trWireEncodeResultError () =
    let text = wireEncodeResult (Error (ToolNotPermitted("a", "t")))
    check "error contains failed" (text.Contains "failed")
    check "error contains not permitted" (text.Contains "not permitted")

let run () =
    idSessionIdRoundtrip (); idWorkspaceIdRoundtrip (); idAgentIdRoundtrip (); idToolIdRoundtrip ()
    idCallIdRoundtrip (); idChildIdRoundtrip ()
    idQuick (); idTryValid (); idTryEmpty ()
    formatAllErrors (); isAbortChecks (); containsAbortChecks (); classifyErrorLeafChecks ()
    workspaceStateEmpty (); workspaceStateReduceRegister (); workspaceStateReduceUnregister ()
    methToolResultText (); methToolResultTextMulti (); methTodoResultTextEmpty (); methTodoResultTextOne ()
    methEnumCount (); methSelectFieldDesc ()
    ompEditorPrompt (); ompGreperPrompt (); ompBrowserPrompt ()
    taRead (); taWrite (); taMeditator (); taBrowser (); taWebsearch (); taWebfetch ()
    taExecutor (); taTodoWrite (); taKnowledgeGraphFetch (); taReturnBookkeeper (); taApplyPatch (); taSubmitReview ()
    trWireEncodeResultOk (); trWireEncodeResultError ()
