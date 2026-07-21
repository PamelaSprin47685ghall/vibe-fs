module Wanxiangshu.Tests.KernelDomainIdentityTests

open Fable.Core
open Fable.Core.JsInterop
open System
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.Primitives.Identity.Id
open Wanxiangshu.Kernel.WorkspaceState
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.MessageSourceClassify
open Wanxiangshu.Kernel.ToolExecutionStatusModule
open Wanxiangshu.Kernel.HostTools

// ── Kernel.Domain ──────────────────────────────────────────────────────────

let domainTryWorkspaceIdSuccess () =
    equal "tryWorkspaceId ok" (Some "ws-1") (tryWorkspaceId "ws-1" |> Option.map workspaceIdValue)

let domainTryWorkspaceIdEmpty () =
    equal "tryWorkspaceId empty" None (tryWorkspaceId "")

let domainTryAgentIdSuccess () =
    equal "tryAgentId ok" (Some "a1") (tryAgentId "a1" |> Option.map agentIdValue)

let domainTryAgentIdEmpty () =
    equal "tryAgentId empty" None (tryAgentId "")

let domainQuickIds () =
    let w = workspaceIdQuick "wq"
    equal "workspaceIdQuick" "wq" (workspaceIdValue w)
    let a = agentIdQuick "aq"
    equal "agentIdQuick" "aq" (agentIdValue a)

let domainFormatAllErrors () =
    equal "MessageAborted" "aborted" (formatDomainError MessageAborted)
    equal "SessionBusy" "session busy" (formatDomainError SessionBusy)
    equal "TaskWaitBackgrounded" "task wait backgrounded" (formatDomainError TaskWaitBackgrounded)

    equal
        "ExecutorExecutableMissing"
        "executable not found: bash"
        (formatDomainError (ExecutorExecutableMissing "bash"))

    equal
        "ParseError"
        "parse error in json: unexpected end of input"
        (formatDomainError (ParseError("json", "unexpected end of input")))

    equal
        "ToolNotPermitted"
        "tool 'write' not permitted for agent 'reader'"
        (formatDomainError (ToolNotPermitted("reader", "write")))

    equal
        "InvalidIntent"
        "invalid arg for tool 'eval': missing"
        (formatDomainError (InvalidIntent("eval", "arg", "missing")))

    equal "UpstreamTimeout" "upstream timeout after 30s" (formatDomainError (UpstreamTimeout 30))

    equal
        "UpstreamRefused"
        "upstream refused: connection reset"
        (formatDomainError (UpstreamRefused "connection reset"))

    equal "SystemPanic" "system panic: invariant violated" (formatDomainError (SystemPanic "invariant violated"))
    equal "UnknownJsError" "some native error" (formatDomainError (UnknownJsError "some native error"))

let domainIsAbort () =
    check "MessageAborted is abort" (isAbort MessageAborted)
    check "SessionBusy not abort" (not (isAbort SessionBusy))
    check "UnknownJsError not abort" (not (isAbort (UnknownJsError "x")))

let domainContainsAbortText () =
    check "aborted" (containsAbortText "aborted")
    check "Abort" (containsAbortText "Abort")
    check "null no" (not (containsAbortText null))
    check "empty no" (not (containsAbortText ""))
    check "hello no" (not (containsAbortText "hello"))

let domainClassifyErrorLeaf () =
    equal "AbortError name" (ClientCancellation "AbortError") (classifyErrorLeaf "AbortError" "SomeTag" "nope")
    equal "MessageAborted tag" MessageAborted (classifyErrorLeaf "SomeError" "MessageAborted" "nope")
    equal "SessionBusyError name" SessionBusy (classifyErrorLeaf "SessionBusyError" "SomeTag" "nope")
    equal "SessionBusy tag" SessionBusy (classifyErrorLeaf "SomeError" "SessionBusy" "nope")

    equal
        "ForegroundWaitBackgroundedError name"
        TaskWaitBackgrounded
        (classifyErrorLeaf "ForegroundWaitBackgroundedError" "SomeTag" "nope")

    equal "TaskWaitBackgrounded tag" TaskWaitBackgrounded (classifyErrorLeaf "SomeError" "TaskWaitBackgrounded" "nope")

    equal
        "fallback abort text"
        (ClientCancellation "abort-text")
        (classifyErrorLeaf "UnknownError" "SomeTag" "operation aborted")

    equal
        "fallback no abort"
        (UnknownJsError "plain failure")
        (classifyErrorLeaf "UnknownError" "SomeTag" "plain failure")

let domainReduce () =
    let cid1 =
        match childId "c1" with
        | Ok id -> id
        | Error msg -> failwith ("childId failed: " + msg)

    let s =
        reduce Wanxiangshu.Kernel.WorkspaceState.empty (ChildRegistered(cid1, { agent = "a"; parentSessionId = None }))

    check "reduce adds child" (Map.count s.childSessions = 1)
    let s2 = reduce s (ChildUnregistered cid1)
    equal "reduce unregister idempotent to empty" Wanxiangshu.Kernel.WorkspaceState.empty s2

// ── Kernel.Messaging ───────────────────────────────────────────────────────

let msgFlatten () =
    let m1 =
        { info =
            { id = "1"
              sessionID = ""
              role = User
              agent = ""
              isError = false
              toolName = ""
              details = null
              time = null }
          parts = [ TextPart "hi" ]
          source = Native
          raw = null }

    let m2 =
        { info =
            { id = "2"
              sessionID = ""
              role = Assistant
              agent = ""
              isError = false
              toolName = ""
              details = null
              time = null }
          parts = [ TextPart "ok"; ToolPart("t", "c1", None, null) ]
          source = Native
          raw = null }

    let flat = flatten [ m1; m2 ]
    equal "flatten count" 3 flat.Length
    let fp0 = flat.[0]
    check "msg0 isUser" fp0.isUser
    equal "msg0 part type" 0 fp0.msgIndex
    equal "msg0 part index" 0 fp0.partIndex
    let fp1 = flat.[1]
    check "msg1 not user" (not fp1.isUser)
    equal "msg1 first part" 0 fp1.partIndex
    let fp2 = flat.[2]
    equal "msg2 tool part" 1 fp2.partIndex

let msgReadAssistantText () =
    let m =
        { info =
            { id = ""
              sessionID = ""
              role = Assistant
              agent = ""
              isError = false
              toolName = ""
              details = null
              time = null }
          parts = [ TextPart "hello"; TextPart "world" ]
          source = Native
          raw = null }

    equal "assistant text" (Some "hello world") (readAssistantText [ m ] 0 " ")

    let emptyMsg =
        { info =
            { id = ""
              sessionID = ""
              role = Assistant
              agent = ""
              isError = false
              toolName = ""
              details = null
              time = null }
          parts = []
          source = Native
          raw = null }

    equal "empty parts" None (readAssistantText [ emptyMsg ] 0 " ")
    equal "startIndex past" None (readAssistantText [ m ] 1 " ")

    let userMsg =
        { info =
            { id = ""
              sessionID = ""
              role = User
              agent = ""
              isError = false
              toolName = ""
              details = null
              time = null }
          parts = [ TextPart "hi" ]
          source = Native
          raw = null }

    equal "no assistant" None (readAssistantText [ userMsg ] 0 " ")

let msgClassifySourceAndDecodeRole () =
    equal "empty source" Native (classifySource "" None None)
    equal "unknown source" Native (classifySource "chat-123" None None)

    match classifySource "caps-synth-user-x" None None with
    | Synthetic k -> equal "synth user kind" "caps-synth-user-" k
    | _ -> failwith "expected Synthetic"

    equal "decode User" User (decodeRole "user")
    equal "decode Assistant" Assistant (decodeRole "assistant")
    equal "decode toolResult" ToolResult (decodeRole "toolResult")
    equal "decode tool-result" ToolResult (decodeRole "tool-result")
    equal "decode tool_result" ToolResult (decodeRole "tool_result")
    equal "decode unknown→System" System (decodeRole "alien")

let msgPartAccessors () =
    let tp = TextPart "hello"
    equal "partTextStr TextPart" "hello" (partTextStr tp)
    check "partIsText TextPart" (partIsText tp)
    check "partIsText not ToolPart" (not (partIsText (ToolPart("t", "c", None, null): Part<obj>)))

    let tool =
        ToolPart(
            "t",
            "c1",
            Some
                { status = fromString ""
                  output = ""
                  error = ""
                  input = null
                  operationAction = "" },
            null
        )

    check "partIsTool ToolPart" (partIsTool tool)
    equal "partCallID" "c1" (partCallID tool)
    check "partIsTool not TextPart" (not (partIsTool tp))
    let updated = setPartOutputTyped tool "new_out"

    match updated with
    | ToolPart(_, _, Some st, _) -> equal "setOutput" "new_out" st.output
    | _ -> failwith "expected ToolPart"

let msgStripSynthetic () =
    let native =
        { info =
            { id = ""
              sessionID = ""
              role = User
              agent = ""
              isError = false
              toolName = ""
              details = null
              time = null }
          parts = []
          source = Native
          raw = null }

    let synth =
        { native with
            source = Synthetic "caps" }

    let r = stripSyntheticBySource [ native; synth ]
    equal "strip synthetic count" 1 r.Length
    equal "stays native" Native r.[0].source

let run () =
    domainTryWorkspaceIdSuccess ()
    domainTryWorkspaceIdEmpty ()
    domainTryAgentIdSuccess ()
    domainTryAgentIdEmpty ()
    domainQuickIds ()
    domainFormatAllErrors ()
    domainIsAbort ()
    domainContainsAbortText ()
    domainClassifyErrorLeaf ()
    domainReduce ()
    msgFlatten ()
    msgReadAssistantText ()
    msgClassifySourceAndDecodeRole ()
    msgPartAccessors ()
    msgStripSynthetic ()
