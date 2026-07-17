module Wanxiangshu.Tests.KernelDomainCoverageTests

open Fable.Core
open Fable.Core.JsInterop
open System
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.Primitives.Identity.Id
open Wanxiangshu.Kernel.WorkspaceState
open Wanxiangshu.Kernel.Methodology.Api
open Wanxiangshu.Kernel.Methodology.Registry

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

    match sid with
    | Ok v -> equal "sessionIdValue" "s1" (sessionIdValue v)
    | _ -> check "sessionId ok" false

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
    equal "AbortError name" (ClientCancellation "AbortError") (classifyErrorLeaf "AbortError" "" "msg")
    equal "MessageAborted tag" MessageAborted (classifyErrorLeaf "" "MessageAborted" "msg")
    equal "SessionBusyError name" SessionBusy (classifyErrorLeaf "SessionBusyError" "" "msg")
    equal "TaskWaitBackgrounded tag" TaskWaitBackgrounded (classifyErrorLeaf "" "TaskWaitBackgrounded" "msg")
    equal "Other+abort text" (ClientCancellation "abort-text") (classifyErrorLeaf "Other" "Other" "abort text")
    equal "Other no abort" (UnknownJsError "normal") (classifyErrorLeaf "Other" "Other" "normal")

// ── Kernel.Domain.WorkspaceState ──────────────────────────────────────────────
let workspaceStateEmpty () =
    check "empty childSessions empty" (Map.isEmpty empty.childSessions)

let workspaceStateReduceRegister () =
    let cid =
        match childId "ch1" with
        | Ok v -> v
        | Error _ -> failwith "childId failed"

    let ev = ChildRegistered(cid, { agent = "a"; parentSessionId = None })
    let s = reduce empty ev
    equal "reduce count 1" 1 (Map.count s.childSessions)

let workspaceStateReduceUnregister () =
    let cid =
        match childId "ch1" with
        | Ok v -> v
        | Error _ -> failwith "childId failed"

    let evReg = ChildRegistered(cid, { agent = "a"; parentSessionId = None })
    let s = reduce empty evReg
    let evUnreg = ChildUnregistered cid
    let s2 = reduce s evUnreg
    check "reduce unregister back to empty" (Map.isEmpty s2.childSessions)

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
    check "todoResultTextDomain has summary clause" (r.Contains "summary" || r.Contains "summarizing")
    check "todoResultTextDomain has difficulty clause" (r.Contains "difficult" || r.Contains "complex")

    check
        "todoResultTextDomain has negative instruction for completion"
        (r.ToLowerInvariant().Contains "not need"
         || r.ToLowerInvariant().Contains "no need")

let methEnumCount () =
    check "enum count > 50" (enumValues.Value.Length > 50)

let methSelectFieldDesc () =
    check
        "selectMethodologyFieldDescription contains select_methodology"
        (selectMethodologyFieldDescription.Contains "select_methodology")

let run () =
    idSessionIdRoundtrip ()
    idWorkspaceIdRoundtrip ()
    idAgentIdRoundtrip ()
    idToolIdRoundtrip ()
    idCallIdRoundtrip ()
    idChildIdRoundtrip ()
    idQuick ()
    idTryValid ()
    idTryEmpty ()
    formatAllErrors ()
    isAbortChecks ()
    containsAbortChecks ()
    classifyErrorLeafChecks ()
    workspaceStateEmpty ()
    workspaceStateReduceRegister ()
    workspaceStateReduceUnregister ()
    methToolResultText ()
    methToolResultTextMulti ()
    methTodoResultTextEmpty ()
    methTodoResultTextOne ()
    methEnumCount ()
    methSelectFieldDesc ()
