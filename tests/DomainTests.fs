module Wanxiangshu.Tests.DomainTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.Domain.Id

// --- formatDomainError ---

let formatDomainErrorMessageAborted () =
    equal "MessageAborted" "aborted" (formatDomainError MessageAborted)

let formatDomainErrorSessionBusy () =
    equal "SessionBusy" "session busy" (formatDomainError SessionBusy)

let formatDomainErrorTaskWaitBackgrounded () =
    equal "TaskWaitBackgrounded" "task wait backgrounded" (formatDomainError TaskWaitBackgrounded)

let formatDomainErrorExecutorExecutableMissing () =
    equal
        "ExecutorExecutableMissing bash"
        "executable not found: bash"
        (formatDomainError (ExecutorExecutableMissing "bash"))

let formatDomainErrorParseError () =
    equal
        "ParseError"
        "parse error in json: unexpected end of input"
        (formatDomainError (ParseError("json", "unexpected end of input")))

let formatDomainErrorToolNotPermitted () =
    equal
        "ToolNotPermitted"
        "tool 'write' not permitted for agent 'reader'"
        (formatDomainError (ToolNotPermitted("reader", "write")))

let formatDomainErrorInvalidIntent () =
    equal
        "InvalidIntent"
        "invalid arg for tool 'eval': missing"
        (formatDomainError (InvalidIntent("eval", "arg", "missing")))

let formatDomainErrorUpstreamTimeout () =
    equal "UpstreamTimeout" "upstream timeout after 30s" (formatDomainError (UpstreamTimeout 30))

let formatDomainErrorUpstreamRefused () =
    equal
        "UpstreamRefused"
        "upstream refused: connection reset"
        (formatDomainError (UpstreamRefused "connection reset"))

let formatDomainErrorSystemPanic () =
    equal "SystemPanic" "system panic: invariant violated" (formatDomainError (SystemPanic "invariant violated"))

let formatDomainErrorUnknownJsError () =
    equal "UnknownJsError passthrough" "some native error" (formatDomainError (UnknownJsError "some native error"))

let formatDomainErrorFileSystemFault () =
    equal
        "FileSystemFault"
        "file system fault: path=/tmp/x, errno=EACCES: permission denied"
        (formatDomainError (FileSystemFault("/tmp/x", "EACCES", "permission denied")))

let formatDomainErrorNetworkTransportFailure () =
    equal
        "NetworkTransportFailure Some status"
        "network transport failure: url=https://api.example.com, status=500, body=server error"
        (formatDomainError (NetworkTransportFailure("https://api.example.com", Some 500, "server error")))

let formatDomainErrorNetworkTransportFailureNone () =
    equal
        "NetworkTransportFailure None status"
        "network transport failure: url=https://api.example.com, status=none, body="
        (formatDomainError (NetworkTransportFailure("https://api.example.com", None, "")))

let formatDomainErrorClientCancellation () =
    equal "ClientCancellation" "client cancelled: user click" (formatDomainError (ClientCancellation "user click"))

let formatDomainErrorHostProtocolMismatch () =
    equal
        "HostProtocolMismatch"
        "host protocol mismatch: field=sessionId, expected=string, actual=number"
        (formatDomainError (HostProtocolMismatch("sessionId", "string", "number")))

// --- isAbort ---

let isAbortTrueForMessageAborted () =
    check "MessageAborted is abort" (isAbort MessageAborted)

let isAbortTrueForClientCancellation () =
    check "ClientCancellation is abort" (isAbort (ClientCancellation "user"))

let isAbortFalseForAllOthers () =
    check "SessionBusy not abort" (not (isAbort SessionBusy))
    check "TaskWaitBackgrounded not abort" (not (isAbort TaskWaitBackgrounded))
    check "ExecutorExecutableMissing not abort" (not (isAbort (ExecutorExecutableMissing "bash")))
    check "ParseError not abort" (not (isAbort (ParseError("ctx", "d"))))
    check "ToolNotPermitted not abort" (not (isAbort (ToolNotPermitted("a", "t"))))
    check "InvalidIntent not abort" (not (isAbort (InvalidIntent("t", "f", "d"))))
    check "UpstreamTimeout not abort" (not (isAbort (UpstreamTimeout 5)))
    check "UpstreamRefused not abort" (not (isAbort (UpstreamRefused "r")))
    check "SystemPanic not abort" (not (isAbort (SystemPanic "m")))
    check "UnknownJsError not abort" (not (isAbort (UnknownJsError "m")))

let isAbortFalseForFileSystemFault () =
    check "FileSystemFault not abort" (not (isAbort (FileSystemFault("/x", "EIO", "io"))))

let isAbortFalseForNetworkTransportFailure () =
    check
        "NetworkTransportFailure not abort"
        (not (isAbort (NetworkTransportFailure("https://api.example.com", None, ""))))

let isAbortFalseForHostProtocolMismatch () =
    check "HostProtocolMismatch not abort" (not (isAbort (HostProtocolMismatch("f", "string", "number"))))

// --- containsAbortText ---

let containsAbortTextLower () =
    check "\"aborted\" is abort text" (containsAbortText "aborted")

let containsAbortTextMixed () =
    check "\"Abort\" is abort text" (containsAbortText "Abort")

let containsAbortTextNull () =
    check "null not abort text" (not (containsAbortText null))

let containsAbortTextEmpty () =
    check "empty string not abort text" (not (containsAbortText ""))

let containsAbortTextNormal () =
    check "\"hello\" not abort text" (not (containsAbortText "hello"))

// --- classifyErrorLeaf ---

let classifyErrorLeafAbortByName () =
    equal
        "AbortError name → ClientCancellation"
        (ClientCancellation "AbortError")
        (classifyErrorLeaf "AbortError" "SomeTag" "nope")

let classifyErrorLeafAbortSignalByName () =
    equal
        "AbortSignal name → ClientCancellation"
        (ClientCancellation "AbortSignal")
        (classifyErrorLeaf "AbortSignal" "SomeTag" "nope")

let classifyErrorLeafAbortByTag () =
    equal "MessageAborted tag → MessageAborted" MessageAborted (classifyErrorLeaf "SomeError" "MessageAborted" "nope")

let classifyErrorLeafSessionBusyByName () =
    equal "SessionBusyError name → SessionBusy" SessionBusy (classifyErrorLeaf "SessionBusyError" "SomeTag" "nope")

let classifyErrorLeafSessionBusyByTag () =
    equal "SessionBusy tag → SessionBusy" SessionBusy (classifyErrorLeaf "SomeError" "SessionBusy" "nope")

let classifyErrorLeafBackgroundedByName () =
    equal
        "ForegroundWaitBackgroundedError name → TaskWaitBackgrounded"
        TaskWaitBackgrounded
        (classifyErrorLeaf "ForegroundWaitBackgroundedError" "SomeTag" "nope")

let classifyErrorLeafBackgroundedByTag () =
    equal
        "TaskWaitBackgrounded tag → TaskWaitBackgrounded"
        TaskWaitBackgrounded
        (classifyErrorLeaf "SomeError" "TaskWaitBackgrounded" "nope")

let classifyErrorLeafFallbackAbortText () =
    equal
        "fallback abort text → ClientCancellation"
        (ClientCancellation "abort-text")
        (classifyErrorLeaf "UnknownError" "SomeTag" "operation aborted")

let classifyErrorLeafHostProtocolMismatchByTag () =
    let msg = "field sessionId type mismatch"

    equal
        "HostProtocolMismatch tag → UnknownJsError fallback"
        (UnknownJsError msg)
        (classifyErrorLeaf "UnknownError" "HostProtocolMismatch" msg)

let classifyErrorLeafFallbackNoAbort () =
    let msg = "plain failure"
    equal "fallback no abort → UnknownJsError" (UnknownJsError msg) (classifyErrorLeaf "UnknownError" "SomeTag" msg)

// --- Id parsers ---

let sessionIdSuccess () =
    match sessionId "abc" with
    | Ok sid -> equal "sessionId wraps" "abc" (sessionIdValue sid)
    | _ -> failwith "sessionId should succeed for non-empty input"

let sessionIdEmptyFailure () =
    match sessionId "" with
    | Error msg -> check "sessionId empty gives error" (msg.Contains "non-empty")
    | _ -> failwith "sessionId should fail on empty input"

let trySessionIdSuccess () =
    equal "trySessionId success" (Some "x") (trySessionId "x" |> Option.map sessionIdValue)

let trySessionIdEmptyFailure () =
    equal "trySessionId empty → None" None (trySessionId "")

let workspaceIdQuickValueRoundTrip () =
    let w = workspaceIdQuick "ws-1"
    equal "workspaceIdQuick round-trip value" "ws-1" (workspaceIdValue w)

let tryAgentIdSuccess () =
    equal "tryAgentId success" (Some "agent-1") (tryAgentId "agent-1" |> Option.map agentIdValue)

let tryAgentIdEmptyFailure () =
    equal "tryAgentId empty → None" None (tryAgentId "")

// --- reduce ---

let reduceEmptyNoChildren () =
    let cid1 =
        match childId "c1" with
        | Ok id -> id
        | Error msg -> failwith ("childId parser failed: " + msg)

    let s =
        reduce empty (ChildRegistered(cid1, { agent = "a"; parentSessionId = None }))

    check "reduce adds one child" (Map.count s.childSessions = 1)
    let s2 = reduce s (ChildUnregistered cid1)
    equal "reduce removes child back to empty" empty s2

let reduceIdempotentUnregister () =
    let ghostId =
        match childId "ghost" with
        | Ok id -> id
        | Error msg -> failwith ("childId parser failed: " + msg)

    equal "unregister on empty is idempotent" empty (reduce empty (ChildUnregistered ghostId))
