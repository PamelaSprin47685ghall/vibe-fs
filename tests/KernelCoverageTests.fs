module Wanxiangshu.Tests.KernelCoverageTests

open Fable.Core
open Fable.Core.JsInterop
open System
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.Domain.Id
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

// ── Kernel.Domain.Id ─────────────────────────────────────────────────────────

let idRoundTrip parser value extract input =
    match parser input with
    | Ok v -> equal value (extract v)
    | Error _ -> check (value + " parse") false

let idError parser input =
    check (input + " error") (Result.isError (parser input))

let idSessionIdRoundtrip () =
    let sid = sessionId "s1"
    equal "sessionId Some" true (Result.isOk sid)
    match sid with Ok v -> equal "sessionIdValue" "s1" (sessionIdValue v) | _ -> check "sessionId ok" false

let idWorkspaceIdRoundtrip () =
    idRoundTrip workspaceId "workspaceId" workspaceIdValue "w1"

let idAgentIdRoundtrip () =
    idRoundTrip agentId "agentId" agentIdValue "a1"

let idToolIdRoundtrip () =
    idRoundTrip toolId "toolId" toolIdValue "t1"

let idCallIdRoundtrip () =
    idRoundTrip callId "callId" callIdValue "c1"

let idChildIdRoundtrip () =
    idRoundTrip childId "childId" childIdValue "ch1"

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
    equal "ParseError" "parse error in d: ctx" (formatDomainError (ParseError(context = "ctx", detail = "d")))
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
    equal "AbortError name" (ClientCancellation "AbortError") (classifyErrorLeaf "AbortError" "" "msg")
    equal "MessageAborted tag" MessageAborted (classifyErrorLeaf "" "MessageAborted" "msg")
    equal "SessionBusyError name" SessionBusy (classifyErrorLeaf "SessionBusyError" "" "msg")
    equal "TaskWaitBackgrounded tag" TaskWaitBackgrounded (classifyErrorLeaf "" "TaskWaitBackgrounded" "msg")
    equal "Other+abort text" (ClientCancellation "abort-text") (classifyErrorLeaf "Other" "Other" "abort text")
    equal "Other no abort" (UnknownJsError "normal") (classifyErrorLeaf "Other" "Other" "normal")

// ── Kernel.Domain.WorkspaceState ──────────────────────────────────────────────let workspaceStateEmpty () =
    check "empty childSessions empty" (Map.isEmpty Domain.empty.childSessions)

let workspaceStateReduceRegister () =
    let cid = match childId "ch1" with Ok v -> v | Error _ -> failwith "childId failed"
    let ev = ChildRegistered(cid, { agent = "a"; parentSessionId = None })
    let s = reduce Domain.empty ev
    equal "reduce count 1" 1 (Map.count s.childSessions)

let workspaceStateReduceUnregister () =
    let cid = match childId "ch1" with Ok v -> v | Error _ -> failwith "childId failed"
    let evReg = ChildRegistered(cid, { agent = "a"; parentSessionId = None })
    let s = reduce Domain.empty evReg
    let evUnreg = ChildUnregistered cid
    let s2 = reduce s evUnreg
    check "reduce unregister back to empty" (Map.isEmpty s2.childSessions)

// ── Kernel.Methodology ────────────────────────────────────────────────────────let methToolResultText () =
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
    check "enum count > 50" (enumValues.Length > 50)

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

// ── Kernel.ReviewReplayPolicy ─────────────────────────────────────────────────
let rrpTextsFromFlatPartsTool () =
    let toolState = { status = "completed"; output = "out"; error = ""; input = null; operationAction = "" }
    let fp = { msgIndex = 0; partIndex = 0; isUser = false; part = ToolPart("t", "c1", Some toolState, null) }
    let texts = textsFromFlatParts [ fp ] |> Seq.toList
    equal "tool output text" ["out"] texts

let rrpTextsFromFlatPartsText () =
    let fp = { msgIndex = 0; partIndex = 0; isUser = false; part = TextPart "hello" }
    let texts = textsFromFlatParts [ fp ] |> Seq.toList
    equal "text part" ["hello"] texts

let rrpTextsFromFlatPartsOther () =
    let fp = { msgIndex = 0; partIndex = 0; isUser = false; part = ToolPart("t", "c", None, null) }
    let texts = textsFromFlatParts [ fp ] |> Seq.toList
    equal "no output tool" [""] texts

// ── Kernel.ReviewSession.Types ────────────────────────────────────────────────
let rstEmpty () =
    let e = empty "rs1" 100L
    equal "empty id" "rs1" e.id
    equal "empty state" ReviewState.Inactive e.state
    equal "empty version" 0 e.version
    equal "empty parentId" None e.parentId

let rstWithTask () =
    let e = empty "rs1" 100L
    let once = withTask "t1" e
    equal "withTask set" (Some "t1") once.originalTask
    equal "withTask version 1" 1 once.version
    let same = withTask "t1" once
    equal "withTask same version" once.version same.version
    let diff = withTask "t2" once
    equal "withTask diff version" 2 diff.version
    equal "withTask diff task" (Some "t2") diff.originalTask

let rstWithFeedback () =
    let e = empty "rs1" 100L
    let fb = withFeedback e "good"
    equal "feedback set" (Some "good") fb.lastFeedback
    let same = withFeedback fb "good"
    equal "feedback same version" fb.version same.version
    let diff = withFeedback fb "bad"
    equal "feedback new version" (fb.version + 1) diff.version
    equal "feedback new text" (Some "bad") diff.lastFeedback

let rstAddChild () =
    let e = empty "rs1" 100L
    let c1 = addChild e "c1"
    equal "addChild new" ["c1"] c1.childIds
    equal "addChild version" (e.version + 1) c1.version
    let dup = addChild c1 "c1"
    equal "addChild dup" ["c1"] dup.childIds
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
    idSessionIdRoundtrip (); idSessionIdEmpty ()
    idWorkspaceIdRoundtrip (); idAgentIdRoundtrip (); idToolIdRoundtrip ()
    idCallIdRoundtrip (); idChildIdRoundtrip ()
    idQuick (); idTryValid (); idTryEmpty ()
    formatAllErrors (); isAbortChecks (); containsAbortChecks (); classifyErrorLeafChecks ()
    workspaceStateEmpty (); workspaceStateReduceRegister (); workspaceStateReduceUnregister ()
    methToolResultText (); methToolResultTextMulti (); methTodoResultTextEmpty (); methTodoResultTextOne ()
    methEnumCount (); methSelectFieldDesc ()
    ompEditorPrompt (); ompGreperPrompt (); ompBrowserPrompt ()
    taRead (); taWrite (); taMeditator (); taBrowser (); taWebsearch (); taWebfetch ()
    taExecutor (); taTodoWrite (); taApplyPatch (); taSubmitReview ()
    trWireEncodeResultOk (); trWireEncodeResultError ()
    rrpTextsFromFlatPartsTool (); rrpTextsFromFlatPartsText (); rrpTextsFromFlatPartsOther ()
    rstEmpty (); rstWithTask (); rstWithFeedback (); rstAddChild ()
    cfgStealthBrowserRef (); cfgStealthBrowserCommand (); cfgStealthBrowserLocalConfig ()
