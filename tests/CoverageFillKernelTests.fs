module Wanxiangshu.Tests.CoverageFillKernelTests

open Fable.Core
open Fable.Core.JsInterop
open System
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.Domain.Id
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.WebFetchGuard
open Wanxiangshu.Kernel.ExecutorStrip
open Wanxiangshu.Kernel.FuzzyPath
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
        reduce Wanxiangshu.Kernel.Domain.empty (ChildRegistered(cid1, { agent = "a"; parentSessionId = None }))

    check "reduce adds child" (Map.count s.childSessions = 1)
    let s2 = reduce s (ChildUnregistered cid1)
    equal "reduce unregister idempotent to empty" Wanxiangshu.Kernel.Domain.empty s2

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
    equal "empty source" Native (classifySource "")
    equal "unknown source" Native (classifySource "chat-123")

    match classifySource "caps-synth-user-x" with
    | Synthetic k -> equal "synth user kind" "caps-synth-user-" k
    | _ -> failwith "expected Synthetic"

    match classifySource "backlog-projection-y" with
    | Synthetic _ -> ()
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
                { status = ""
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

// ── Kernel.WebFetchGuard ───────────────────────────────────────────────────

let wfgValidateUrl () =
    match validateFetchUrl "http://example.com" with
    | Ok() -> check "http ok" true
    | Error _ -> check "http ok" false

    match validateFetchUrl "https://example.com/path" with
    | Ok() -> check "https ok" true
    | Error _ -> check "https ok" false

    match validateFetchUrl "http://[::1]" with
    | Error msg -> equal "ipv6 literal blocked" "host not allowed" msg
    | Ok() -> check "ipv6 blocked" false

    match validateFetchUrl "not a url" with
    | Error msg -> equal "invalid url" "invalid URL" msg
    | Ok() -> check "invalid blocked" false

    match validateFetchUrl "ftp://example.com" with
    | Error msg -> equal "ftp unsupported" "unsupported URL scheme: ftp" msg
    | Ok() -> check "ftp blocked" false

    match validateFetchUrl "http://localhost" with
    | Error msg -> equal "localhost blocked" "host not allowed" msg
    | Ok() -> check "localhost blocked" false

    match validateFetchUrl "http://127.0.0.1" with
    | Error msg -> equal "private ipv4 blocked" "host not allowed" msg
    | Ok() -> check "loopback blocked" false

    match validateFetchUrl "http://8.8.8.8" with
    | Ok() -> check "public ip ok" true
    | Error _ -> check "public ip ok" false

// ── Kernel.ExecutorStrip ───────────────────────────────────────────────────

let stripHeadTailPipes () =
    let r = strip "cat file | head -n 5 | tail -n 2"
    equal "head-tail stripped" "cat file" r.script
    equal "head-tail count" 2 r.stripped.Length
    equal "first name" "head" r.stripped.[0].name
    equal "first count" 5 r.stripped.[0].count
    equal "second name" "tail" r.stripped.[1].name
    equal "second count" 2 r.stripped.[1].count

let stripSingleQuotes () =
    let r = strip "echo 'hello world' | head -n 3"
    equal "single-quote preserved" "echo 'hello world'" r.script
    equal "head extracted" 1 r.stripped.Length
    equal "head count" 3 r.stripped.[0].count

let stripDoubleQuotes () =
    let r = strip """echo "pipe|here" | tail -n 1"""
    check "double-quote preserved" (r.script.Contains "\"pipe|here\"")
    equal "tail count" 1 r.stripped.[0].count

let stripComment () =
    let r = strip "cat file | head -n 5 # skip rest"
    equal "comment kept" "cat file # skip rest" r.script
    equal "head extracted" 1 r.stripped.Length

let stripNoPipe () =
    let r = strip "cat file"
    equal "no change" "cat file" r.script
    check "no stripped" (List.isEmpty r.stripped)

let stripUnsupportedCommand () =
    let r = strip "cat file | grep 5"
    equal "unsupported unchanged" "cat file | grep 5" r.script
    check "no stripped" (List.isEmpty r.stripped)

// ── Kernel.FuzzyPath ───────────────────────────────────────────────────────

let fpNormalizePathConstraint () =
    let cwd = "/workspace"
    equal "empty→None" None (normalizePathConstraint "" cwd)
    equal "dot→None" None (normalizePathConstraint "." cwd)
    equal "src→Some src/" (Some "src/") (normalizePathConstraint "src" cwd)
    equal "src slash" (Some "src/") (normalizePathConstraint "src/" cwd)
    equal "abs within" (Some "src/") (normalizePathConstraint "/workspace/src" cwd)
    equal "abs outside→None" None (normalizePathConstraint "/other" cwd)
    equal "recursive glob" (Some "src/") (normalizePathConstraint "src/**" cwd)
    equal "glob fs" (Some "src/*.fs") (normalizePathConstraint "src/*.fs" cwd)

let fpBuildQuery () =
    let cwd = "/workspace"
    equal "no path no exclude" "foo" (buildQuery None "foo" [] cwd false)
    equal "path+exclude+pattern" "src/ !node_modules/ foo" (buildQuery (Some "src") "foo" [ "node_modules" ] cwd false)
    equal "external abs" "/ext/file p" (buildQuery (Some "/ext/file") "p" [] cwd true)

let fpResolveSearchPath () =
    let cwd = "/workspace"
    let r0 = resolveFuzzySearchPath None cwd
    check "none: cwd base" (r0.basePath = cwd)
    check "none: no constraint" r0.pathConstraint.IsNone
    check "none: not external" (not r0.external)
    let r1 = resolveFuzzySearchPath (Some "src") cwd
    check "src: cwd base" (r1.basePath = cwd)
    equal "src constraint" (Some "src") r1.pathConstraint
    let r2 = resolveFuzzySearchPath (Some "/ext") cwd
    check "ext: external base" (r2.basePath = "/ext")
    check "ext: no constraint" r2.pathConstraint.IsNone
    check "ext: is external" r2.external
    let r3 = resolveFuzzySearchPath (Some "/ext/file.txt") cwd
    equal "ext-file: base" "/ext" r3.basePath
    equal "ext-file: constraint" (Some "file.txt") r3.pathConstraint

let fpResolveExternalPath () =
    let cwd = "/workspace"
    let (b0, c0) = resolveExternalPath (Some "src") cwd
    check "internal: base none" b0.IsNone
    check "internal: constraint none" c0.IsNone
    let (b1, c1) = resolveExternalPath (Some "/ext") cwd
    equal "ext base" (Some "/ext") b1
    check "ext no constraint" c1.IsNone
    let (b2, c2) = resolveExternalPath (Some "/ext/file.txt") cwd
    equal "ext-file base" (Some "/ext") b2
    equal "ext-file constraint" (Some "file.txt") c2

let fpResolveExternalBasePathForTest () =
    let r1 = resolveExternalBasePathForTest "/ext"
    equal "dir base" "/ext" r1.basePath
    check "dir no constraint" r1.pathConstraint.IsNone
    let r2 = resolveExternalBasePathForTest "/ext/file.txt"
    equal "file base" "/ext" r2.basePath
    equal "file constraint" (Some "file.txt") r2.pathConstraint

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
    wfgValidateUrl ()
    stripHeadTailPipes ()
    stripSingleQuotes ()
    stripDoubleQuotes ()
    stripComment ()
    stripNoPipe ()
    stripUnsupportedCommand ()
    fpNormalizePathConstraint ()
    fpBuildQuery ()
    fpResolveSearchPath ()
    fpResolveExternalPath ()
    fpResolveExternalBasePathForTest ()
