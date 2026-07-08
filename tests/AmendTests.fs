module Wanxiangshu.Tests.AmendTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.AmendFilter
open Wanxiangshu.Kernel.Messaging

// ---- Test helpers ----

let msg role toolName callID raw : Message<obj> =
    { info =
        { id = ""
          sessionID = ""
          role = role
          agent = ""
          isError = false
          toolName = toolName
          details = null
          time = null }
      parts = []
      source = Native
      raw = raw }

let userMsg (raw: obj) : Message<obj> =
    msg User "" "" raw

let assistantMsg (parts: Part<obj> list) (raw: obj) : Message<obj> =
    { info =
        { id = ""
          sessionID = ""
          role = Assistant
          agent = ""
          isError = false
          toolName = ""
          details = null
          time = null }
      parts = parts
      source = Native
      raw = raw }

let toolResultMsg (toolName: string) (raw: obj) : Message<obj> =
    msg ToolResult toolName "" raw

let toolPart (callID: string) (toolName: string) : Part<obj> =
    ToolPart(toolName, callID, None, null)

// ---- Extractor ----

let amendExtractor (raw: obj) : int option =
    if isNull raw then None
    else
        let v = raw?amend
        if isNull v then None
        elif Fable.Core.JsInterop.jsTypeof v = "number" then Some(int(unbox<float> v))
        elif Fable.Core.JsInterop.jsTypeof v = "string" then
            match System.Int32.TryParse(string v) with
            | true, x -> Some x
            | _ -> None
        else None

// ---- Tests ----

let testNoAmendPreservesMessages () =
    let msgs =
        [ userMsg (createObj [ "text", box "hello" ])
          assistantMsg [ toolPart "call-1" "read" ] (createObj [])
          toolResultMsg "read" (createObj []) ]
    let result = filterAmendMessages amendExtractor msgs
    equal "no amend: 3 msgs preserved" 3 (List.length result)

let testAmend1PopsOneToolCall () =
    let msgs =
        [ userMsg (createObj [ "text", box "read file" ])
          assistantMsg [ toolPart "call-1" "read" ] (createObj [])
          toolResultMsg "read" (createObj [])
          userMsg (createObj [ "amend", box 1 ]) ]
    let result = filterAmendMessages amendExtractor msgs
    equal "amend=1: only amend msg remains" 1 (List.length result)
    check "amend=1: remaining is amend msg" (result.[0].info.role = User)

let testAmend2PopsTwoToolCalls () =
    let msgs =
        [ userMsg (createObj [ "text", box "step 1" ])
          assistantMsg [ toolPart "call-1" "read" ] (createObj [])
          toolResultMsg "read" (createObj [])
          userMsg (createObj [ "text", box "step 2" ])
          assistantMsg [ toolPart "call-2" "write" ] (createObj [])
          toolResultMsg "write" (createObj [])
          userMsg (createObj [ "amend", box 2 ]) ]
    let result = filterAmendMessages amendExtractor msgs
    equal "amend=2: only amend msg remains" 1 (List.length result)

let testAmendExceedsAvailable () =
    let msgs =
        [ userMsg (createObj [ "text", box "step 1" ])
          assistantMsg [ toolPart "call-1" "read" ] (createObj [])
          toolResultMsg "read" (createObj [])
          userMsg (createObj [ "amend", box 10 ]) ]
    let result = filterAmendMessages amendExtractor msgs
    equal "amend exceeds: 1 msg remains" 1 (List.length result)

let testAmendZeroIgnored () =
    let msgs =
        [ userMsg (createObj [ "text", box "hello" ])
          assistantMsg [ toolPart "call-1" "read" ] (createObj [])
          toolResultMsg "read" (createObj []) ]
    let result = filterAmendMessages amendExtractor msgs
    equal "amend absent: 3 msgs preserved" 3 (List.length result)

let testPopOneToolCallDirect () =
    let msgs =
        [ userMsg (createObj [ "text", box "prompt" ])
          assistantMsg [ toolPart "call-1" "read" ] (createObj [])
          toolResultMsg "read" (createObj []) ]
    let (removed, remaining) = popOneToolCall msgs
    equal "popOne: 3 removed" 3 (List.length removed)
    equal "popOne: 0 remaining" 0 (List.length remaining)

let testPopOneToolCallMultipleChains () =
    let msgs =
        [ userMsg (createObj [ "text", box "step 1" ])
          assistantMsg [ toolPart "call-1" "read" ] (createObj [])
          toolResultMsg "read" (createObj [])
          userMsg (createObj [ "text", box "step 2" ])
          assistantMsg [ toolPart "call-2" "write" ] (createObj [])
          toolResultMsg "write" (createObj []) ]
    let (removed, remaining) = popOneToolCall msgs
    // Last tool call chain: user prompt + assistant tool + tool result
    equal "popOne multi: 3 removed" 3 (List.length removed)
    equal "popOne multi: 3 remaining" 3 (List.length remaining)

let testPopUntilCallID2 () =
    let msgs =
        [ userMsg (createObj [ "text", box "step 1" ])
          assistantMsg [ toolPart "call-1" "read" ] (createObj [])
          toolResultMsg "read" (createObj [])
          userMsg (createObj [ "text", box "step 2" ])
          assistantMsg [ toolPart "call-2" "write" ] (createObj [])
          toolResultMsg "write" (createObj []) ]
    let (removed, remaining) = popUntilCallID 2 msgs
    equal "popUntil 2: 6 removed" 6 (List.length removed)
    equal "popUntil 2: 0 remaining" 0 (List.length remaining)

let testEmptyList () =
    let (removed, remaining) = popOneToolCall []
    equal "popOne empty: 0 removed" 0 (List.length removed)
    equal "popOne empty: 0 remaining" 0 (List.length remaining)
    let result = filterAmendMessages amendExtractor []
    equal "filter empty: 0 result" 0 (List.length result)

let testNoToolCalls () =
    let msgs =
        [ userMsg (createObj [ "text", box "hello" ])
          userMsg (createObj [ "text", box "world" ]) ]
    let (removed, remaining) = popOneToolCall msgs
    equal "popOne no tools: 0 removed" 0 (List.length removed)
    equal "popOne no tools: 2 remaining" 2 (List.length remaining)

let testGetCallIDs () =
    let m = assistantMsg [ toolPart "call-abc" "read"; toolPart "call-def" "write" ] (createObj [])
    let ids = getCallIDs m
    equal "getCallIDs extracts 2" 2 (List.length ids)
    check "getCallIDs first" ((List.head ids) = "call-abc")

let testSingleCallID () =
    let m = assistantMsg [ toolPart "call-only" "read" ] (createObj [])
    let ids = getCallIDs m
    equal "single callID" 1 (List.length ids)
    equal "single callID value" "call-only" (List.head ids)

// ---- Nested amend: an amend marker itself gets popped by a later amend ----

let testNestedAmend () =
    // Scenario: user issues amend=1 (pops call-1 chain), then amend=1 again.
    // The second amend should pop the first amend's user message + call-2 chain,
    // leaving only the final amend message.
    let msgs =
        [ userMsg (createObj [ "text", box "step 1" ])
          assistantMsg [ toolPart "call-1" "read" ] (createObj [])
          toolResultMsg "read" (createObj [])
          userMsg (createObj [ "amend", box 1 ])
          userMsg (createObj [ "text", box "step 2" ])
          assistantMsg [ toolPart "call-2" "write" ] (createObj [])
          toolResultMsg "write" (createObj [])
          userMsg (createObj [ "amend", box 1 ]) ]
    let result = filterAmendMessages amendExtractor msgs
    // After first amend=1: pops call-1 chain → [amend=1 user, step2 user, call-2 assistant, call-2 result, amend=1 user]
    // After second amend=1: pops the last tool call chain (call-2) → [amend=1 user, amend=1 user]
    // Both amend markers remain as user messages (they are not tool calls themselves).
    equal "nested amend: 2 msgs remain (both amend markers)" 2 (List.length result)
    check "nested amend: first is amend marker" (result.[0].info.role = User)
    check "nested amend: second is amend marker" (result.[1].info.role = User)

// ---- Parallel tool calls: single assistant message with multiple ToolParts ----

let testParallelToolCalls () =
    // One assistant message fires two tools in parallel (call-a, call-b),
    // followed by two ToolResult messages. popOneToolCall should treat
    // the entire assistant message + both results as a single chain.
    let msgs =
        [ userMsg (createObj [ "text", box "do both" ])
          assistantMsg [ toolPart "call-a" "read"; toolPart "call-b" "write" ] (createObj [])
          toolResultMsg "read" (createObj [])
          toolResultMsg "write" (createObj [])
          userMsg (createObj [ "text", box "next" ]) ]
    let (removed, remaining) = popOneToolCall msgs
    // The parallel chain = user prompt + assistant (2 ToolParts) + 2 results = 4 messages
    equal "parallel: 4 removed" 4 (List.length removed)
    equal "parallel: 1 remaining" 1 (List.length remaining)
    check "parallel: remaining is last user" (remaining.[0].info.role = User)

let runAll () : unit =
    timed "testNoAmendPreservesMessages" testNoAmendPreservesMessages
    timed "testAmend1PopsOneToolCall" testAmend1PopsOneToolCall
    timed "testAmend2PopsTwoToolCalls" testAmend2PopsTwoToolCalls
    timed "testAmendExceedsAvailable" testAmendExceedsAvailable
    timed "testAmendZeroIgnored" testAmendZeroIgnored
    timed "testPopOneToolCallDirect" testPopOneToolCallDirect
    timed "testPopOneToolCallMultipleChains" testPopOneToolCallMultipleChains
    timed "testPopUntilCallID2" testPopUntilCallID2
    timed "testEmptyList" testEmptyList
    timed "testNoToolCalls" testNoToolCalls
    timed "testGetCallIDs" testGetCallIDs
    timed "testSingleCallID" testSingleCallID
    timed "testNestedAmend" testNestedAmend
    timed "testParallelToolCalls" testParallelToolCalls
