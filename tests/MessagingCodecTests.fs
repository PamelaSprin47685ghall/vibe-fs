module Wanxiangshu.Tests.MessagingCodecTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ToolExecutionStatusModule
open Wanxiangshu.Runtime.MessagingCodec

let muxPartStateToKernelStatusMaps () =
    check
        "output-available -> completed"
        (muxPartStateToKernelStatus "output-available" = ToolExecutionStatus.Completed)

    check "input-available -> pending" (muxPartStateToKernelStatus "input-available" = ToolExecutionStatus.Pending)
    check "passthrough other" (muxPartStateToKernelStatus "running" = ToolExecutionStatus.Unknown "running")

let toolOutputAndErrorFromHostOutputString () =
    let out, err = toolOutputAndErrorFromHostOutput (box "plain text")
    check "string output" (out = "plain text")
    check "string no error" (err = "")

let toolOutputAndErrorFromHostOutputObject () =
    let output = createObj [ "content", box "body"; "error", box "fail msg" ]
    let out, err = toolOutputAndErrorFromHostOutput output
    check "object content" (out = "body")
    check "object error" (err = "fail msg")

let toolOutputAndErrorFromHostOutputNull () =
    let out, err = toolOutputAndErrorFromHostOutput null
    check "null output empty" (out = "")
    check "null error empty" (err = "")

let decodeMuxDynamicToolStateNoneWhenEmpty () =
    let part = createObj [ "state", box "output-available" ]

    match decodeMuxDynamicToolState part with
    | None -> check "empty mux part" true
    | Some _ -> check "empty mux part" false

let decodeMuxDynamicToolStateSomeWithOutput () =
    let part =
        createObj
            [ "state", box "output-available"
              "output", box "tool result"
              "input", box (createObj [ "operation", box (createObj [ "action", box "read" ]) ]) ]

    match decodeMuxDynamicToolState part with
    | Some st ->
        check "mux status mapped" (st.status = ToolExecutionStatus.Completed)
        check "mux output" (st.output = "tool result")
        check "mux operation action" (st.operationAction = "read")
    | None -> check "mux with output" false

let decodeOpencodeToolStateBoxSomeWithFields () =
    let state =
        createObj
            [ "status", box "completed"
              "output", box "out"
              "error", box "err"
              "input", box (createObj [ "operation", box (createObj [ "action", box "apply" ]) ]) ]

    match decodeOpencodeToolStateBox state with
    | Some st ->
        check "opencode status" (st.status = ToolExecutionStatus.Completed)
        check "opencode output" (st.output = "out")
        check "opencode error" (st.error = "err")
        check "opencode operation action" (st.operationAction = "apply")
    | None -> check "opencode state box" false

let decodeOpencodeToolStateBoxNoneWhenNull () =
    match decodeOpencodeToolStateBox null with
    | None -> check "null opencode state" true
    | Some _ -> check "null opencode state" false

let operationActionFromInputNestedOperation () =
    let input = createObj [ "operation", box (createObj [ "action", box "write" ]) ]
    check "nested operation action" (operationActionFromInput input = "write")

let operationActionFromInputEmptyWhenNull () =
    check "null input action" (operationActionFromInput null = "")

let operationActionFromInputEmptyWhenNoOperation () =
    let input = createObj [ "path", box "/tmp" ]
    check "missing operation" (operationActionFromInput input = "")

let operationActionFromInputEmptyWhenNoAction () =
    let input = createObj [ "operation", box (createObj [ "kind", box "patch" ]) ]
    check "missing action" (operationActionFromInput input = "")

let decodeTextPartReadsField () =
    let part = createObj [ "type", box "text"; "text", box "hello codec" ]
    check "decode text field" (decodeTextPart part = "hello codec")

let decodeTextPartMissingReturnsEmpty () =
    let part = createObj [ "type", box "text" ]
    check "missing text field" (decodeTextPart part = "")

let decodePartsFromArrayNull () =
    let arr = decodePartsFromArray null
    check "null parts length" (arr.Length = 0)

let decodePartsFromArrayEmpty () =
    let arr = decodePartsFromArray (box [||])
    check "empty parts length" (arr.Length = 0)

let decodePartsFromArrayTwoElements () =
    let p0 = createObj [ "text", box "a" ]
    let p1 = createObj [ "text", box "b" ]
    let arr = decodePartsFromArray (box [| p0; p1 |])
    check "two parts length" (arr.Length = 2)
    check "first part text" (decodeTextPart arr.[0] = "a")
    check "second part text" (decodeTextPart arr.[1] = "b")

let decodePartsFromArrayNonArrayReturnsEmpty () =
    let arr = decodePartsFromArray (box "not-an-array")
    check "non-array parts length" (arr.Length = 0)

let run () =
    muxPartStateToKernelStatusMaps ()
    toolOutputAndErrorFromHostOutputString ()
    toolOutputAndErrorFromHostOutputObject ()
    toolOutputAndErrorFromHostOutputNull ()
    decodeMuxDynamicToolStateNoneWhenEmpty ()
    decodeMuxDynamicToolStateSomeWithOutput ()
    decodeOpencodeToolStateBoxSomeWithFields ()
    decodeOpencodeToolStateBoxNoneWhenNull ()
    operationActionFromInputNestedOperation ()
    operationActionFromInputEmptyWhenNull ()
    operationActionFromInputEmptyWhenNoOperation ()
    operationActionFromInputEmptyWhenNoAction ()
    decodeTextPartReadsField ()
    decodeTextPartMissingReturnsEmpty ()
    decodePartsFromArrayNull ()
    decodePartsFromArrayEmpty ()
    decodePartsFromArrayTwoElements ()
    decodePartsFromArrayNonArrayReturnsEmpty ()
