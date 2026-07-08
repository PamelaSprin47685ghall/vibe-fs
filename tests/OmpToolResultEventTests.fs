module Wanxiangshu.Tests.OmpToolResultEventTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Omp.ToolResultEvent

module Dyn = Wanxiangshu.Shell.Dyn

let getToolInputPrefersInputOverArgs () =
    let event = createObj [ "input", box "from-input"; "args", box "from-args" ]
    equal "input wins over args" "from-input" (string (getToolInput event))

let getToolInputFallsBackToArgs () =
    let event = createObj [ "args", box "from-args" ]
    equal "no input → args" "from-args" (string (getToolInput event))

let getToolInputReturnsNullishWhenNeitherPresent () =
    let event = createObj []
    check "no input or args → nullish" (Dyn.isNullish (getToolInput event))

let getToolCallIdPrefersToolCallId () =
    let event =
        createObj [ "toolCallId", box "tc-1"; "callId", box "c-1"; "callID", box "cID-1" ]

    equal "toolCallId wins" "tc-1" (getToolCallId event)

let getToolCallIdFallsBackToCallId () =
    let event = createObj [ "callId", box "c-1"; "callID", box "cID-1" ]
    equal "callId wins over callID" "c-1" (getToolCallId event)

let getToolCallIdFallsBackToCallID () =
    let event = createObj [ "callID", box "cID-1" ]
    equal "callID used" "cID-1" (getToolCallId event)

let getToolCallIdReturnsEmptyWhenNonePresent () =
    let event = createObj []
    equal "no id fields → empty" "" (getToolCallId event)

let getToolResultTextFromContentArray () =
    let content = [| createObj [ "type", box "text"; "text", box "hello" ] |]
    let event = createObj [ "content", box content ]
    equal "array content → text joined" "hello" (getToolResultText event)

let getToolResultTextFromContentArrayMixed () =
    let content =
        [| createObj [ "type", box "text"; "text", box "hello " ]
           "plain-string-segment"
           createObj [ "type", box "text"; "text", box "world" ] |]

    let event = createObj [ "content", box content ]
    equal "mixed array content" "hello plain-string-segmentworld" (getToolResultText event)

let getToolResultTextFromStringContent () =
    let event = createObj [ "content", box "plain text" ]
    equal "string content → as-is" "plain text" (getToolResultText event)

let setToolResultTextLeavesReadableText () =
    let event = createObj []
    setToolResultText event "hello world"
    equal "round-trip readable" "hello world" (getToolResultText event)

let setToolResultTextPreservesArrayForm () =
    let event =
        createObj [ "content", box [| createObj [ "type", box "text"; "text", box "old" ] |] ]

    setToolResultText event "new text"
    equal "overwrite preserves array readability" "new text" (getToolResultText event)

let setToolResultTextContentIsStringAfterWrite () =
    let event = createObj []
    setToolResultText event "text via legacy field"
    // After setToolResultText the content field is an array [{ type: "text", text: … }]
    let content = Dyn.get event "content"
    check "content is array after write" (Dyn.isArray content)
    equal "getToolResultText returns expected text" "text via legacy field" (getToolResultText event)
