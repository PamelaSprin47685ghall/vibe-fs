module Wanxiangshu.Tests.AmendTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.AmendFilter
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Opencode.HookSchemaDecode
open Wanxiangshu.Shell.MuxPluginCatalogShell
open Wanxiangshu.Shell.MuxToolDefinition
open Wanxiangshu.Omp.OmpToolSchema

module Dyn = Wanxiangshu.Shell.Dyn

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

let userMsg (raw: obj) : Message<obj> = msg User "" "" raw

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

let toolResultMsg (toolName: string) (raw: obj) : Message<obj> = msg ToolResult toolName "" raw
let toolPart (callID: string) (toolName: string) : Part<obj> = ToolPart(toolName, callID, None, null)

let amendExtractor (raw: obj) : int option =
    if isNull raw then
        None
    else
        let v = raw?amend

        if isNull v then
            None
        elif jsTypeof v = "number" then
            Some(int (unbox<float> v))
        elif jsTypeof v = "string" then
            match System.Int32.TryParse(string v) with
            | true, x -> Some x
            | _ -> None
        else
            None

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
    let m =
        assistantMsg [ toolPart "call-abc" "read"; toolPart "call-def" "write" ] (createObj [])

    let ids = getCallIDs m
    equal "getCallIDs extracts 2" 2 (List.length ids)
    check "getCallIDs first" ((List.head ids) = "call-abc")

let testSingleCallID () =
    let m = assistantMsg [ toolPart "call-only" "read" ] (createObj [])
    let ids = getCallIDs m
    equal "single callID" 1 (List.length ids)
    equal "single callID value" "call-only" (List.head ids)

let testNestedAmend () =
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
    equal "nested amend: 2 msgs remain (both amend markers)" 2 (List.length result)
    check "nested amend: first is amend marker" (result.[0].info.role = User)
    check "nested amend: second is amend marker" (result.[1].info.role = User)

let testParallelToolCalls () =
    let msgs =
        [ userMsg (createObj [ "text", box "do both" ])
          assistantMsg [ toolPart "call-a" "read"; toolPart "call-b" "write" ] (createObj [])
          toolResultMsg "read" (createObj [])
          toolResultMsg "write" (createObj [])
          userMsg (createObj [ "text", box "next" ]) ]

    let (removed, remaining) = popOneToolCall msgs
    equal "parallel: 4 removed" 4 (List.length removed)
    equal "parallel: 1 remaining" 1 (List.length remaining)
    check "parallel: remaining is last user" (remaining.[0].info.role = User)

// ---- Bug-exposing test: delayed ToolResult with raw-only callID ----
// Msg 1: User prompt
// Msg 2: Assistant calls "call-1" (F# ToolPart)
// Msg 3: ToolResult for "call-1" (raw callID="call-1", empty F# parts)
// Msg 4: Assistant calls "call-2" (F# ToolPart)
// Msg 5: ToolResult for "call-2" (raw callID="call-2", empty F# parts)
// Msg 6: ToolResult for "call-1" (delayed, raw callID="call-1", empty F# parts)
// Bug: when popping call-2, Msg 6 has empty F# parts so getCallIDs returns [],
//   making the condition `List.isEmpty (getCallIDs m)` true -> Msg 6 swept in incorrectly.
let testAmendPopsCorrectToolResultByRawCallID () =
    let msgs =
        [ userMsg (createObj [ "text", box "step 1" ])
          assistantMsg [ toolPart "call-1" "read" ] (createObj [])
          toolResultMsg "read" (createObj [ "callID", box "call-1" ])
          assistantMsg [ toolPart "call-2" "write" ] (createObj [])
          toolResultMsg "write" (createObj [ "callID", box "call-2" ])
          toolResultMsg "read" (createObj [ "callID", box "call-1" ]) ]

    let (removed, remaining) = popOneToolCall msgs
    // Correct behavior: pop only call-2 chain = [Msg 4, Msg 5]
    //   removed = 2, remaining = [Msg 1, Msg 2, Msg 3, Msg 6] = 4
    // Bug behavior: Msg 6 swept in -> removed = 3, remaining = 3
    equal "raw-callID pop: 2 removed" 2 (List.length removed)
    equal "raw-callID pop: 4 remaining" 4 (List.length remaining)

    // removed[1] must be ToolResult for call-2
    equal "raw-callID pop: removed[1] is ToolResult" ToolResult (removed.[1].info.role)
    let removedCallID = Dyn.get removed.[1].raw "callID" |> string
    equal "raw-callID pop: removed ToolResult is call-2" "call-2" removedCallID

    // remaining must contain Msg 6 (call-1 delayed result) -- it must NOT be swept
    let lastRemaining = remaining.[List.length remaining - 1]
    equal "raw-callID pop: last remaining is ToolResult" ToolResult (lastRemaining.info.role)
    let lastCallID = Dyn.get lastRemaining.raw "callID" |> string
    equal "raw-callID pop: last remaining is call-1 (delayed)" "call-1" lastCallID

let testAmendSchemaInjected () =
    let opencodeSchema =
        createObj [ "type", box "object"; "properties", createObj [ "name", box (createObj []) ] ]

    let opencodeResult = injectAmendIntoJsonSchema opencodeSchema
    let opencodeProps = Dyn.get opencodeResult "properties"
    check "opencode schema has amend property" (not (Dyn.isNullish (Dyn.get opencodeProps "amend")))
    let amendProp = Dyn.get opencodeProps "amend"
    equal "opencode amend type" "integer" (string (Dyn.get amendProp "type"))
    check "opencode amend minimum = 1" (string (Dyn.get amendProp "minimum") = "1")

    let muxTool =
        { name = "coder"
          description = "test"
          parameters = mkSchema (createObj [ "file", box (createObj []) ]) [| "file" |]
          execute = (fun _ _ -> failwith "not implemented")
          condition = (None: (obj -> bool) option) }

    let muxResult = injectAmendIntoMuxSchema muxTool
    let muxProps = muxResult.parameters.properties
    check "mux schema has amend property" (not (Dyn.isNullish (Dyn.get muxProps "amend")))
    let muxAmend = Dyn.get muxProps "amend"
    equal "mux amend type" "integer" (string (Dyn.get muxAmend "type"))

    let ompSchema = createObj [ "properties", createObj [ "file", box (createObj []) ] ]
    let ompResult = injectAmendIntoOmpParameters ompSchema
    let ompProps = Dyn.get ompResult "properties"
    check "omp schema has amend property" (not (Dyn.isNullish (Dyn.get ompProps "amend")))
    let ompAmend = Dyn.get ompProps "amend"
    equal "omp amend type" "integer" (string (Dyn.get ompAmend "type"))

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
    timed "testAmendPopsCorrectToolResultByRawCallID" testAmendPopsCorrectToolResultByRawCallID
    timed "testAmendSchemaInjected" testAmendSchemaInjected
