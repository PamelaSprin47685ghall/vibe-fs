module VibeFs.Tests.BacktrackTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.BacktrackCodec
open VibeFs.Kernel.BacktrackProjector
open VibeFs.Kernel.SyntheticIds

let private userMsg (id: string) (text: string) : obj =
    createObj [
        "info", box (createObj [ "id", box id; "role", box "user"; "sessionID", box "test" ])
        "parts", box [| box {| ``type`` = "text"; text = text |} |]
    ]

let private toolResultMsg (id: string) (callID: string) (toolName: string) (output: string) : obj =
    createObj [
        "info", box (createObj [ "id", box id; "role", box "assistant"; "sessionID", box "test" ])
        "parts", box [| createObj [
            "type", box "tool"; "tool", box toolName; "callID", box callID
            "state", box (createObj [ "status", box "completed"; "input", box (createObj []); "output", box output ])
        ] |]
    ]

let private backtrackMsg (id: string) (callID: string) (anchor: int) (note: string) : obj =
    createObj [
        "info", box (createObj [ "id", box id; "role", box "assistant"; "sessionID", box "test" ])
        "parts", box [| createObj [
            "type", box "tool"; "tool", box "backtrack"; "callID", box callID
            "state", box (createObj [ "status", box "completed"; "input", box (createObj [ "anchor", box anchor; "note", box note ]); "output", box "ok" ])
        ] |]
    ]

let private partOutput (msg: obj) : string =
    let parts = get msg "parts" :?> obj array
    let state = get parts.[0] "state"
    string (get state "output")

let codec () =
    let encoded = encodeId 5 "hello"
    check "encodeId has id" (encoded.StartsWith "#id_: 5")
    check "encodeId has comment" (encoded.Contains "backtrack")
    check "encodeId has output" (encoded.Contains "hello")
    check "tryParseId valid" (tryParseId (encodeId 3 "body") = Some 3)
    check "tryParseId missing" (tryParseId "no prefix" = None)
    check "stripIdPrefix" (stripIdPrefix "#id_: 7\ntext" = "text")
    check "stripIdPrefix no match" (stripIdPrefix "plain" = "plain")
    check "maxIdFromOutputs" (maxIdFromOutputs ["#id_: 1\na"; "#id_: 5\nb"; "plain"] = 5)
    check "maxIdFromOutputs empty" (maxIdFromOutputs [] = 0)

let noBacktrackPassthrough () =
    let msgs = [|
        toolResultMsg "a1" "c0" "read" (encodeId 0 "r0")
        toolResultMsg "a2" "c1" "read" (encodeId 1 "r1")
    |]
    check "no bt: same ref" (obj.ReferenceEquals(project msgs, msgs))

let singleBacktrackRewrite () =
    let msgs = [|
        userMsg "u1" "start"
        toolResultMsg "a1" "c0" "read" (encodeId 0 "result0")
        toolResultMsg "a2" "c1" "read" (encodeId 1 "result1")
        toolResultMsg "a3" "c2" "read" (encodeId 2 "result2")
        backtrackMsg "a4" "bt" 1 "fixed"
        toolResultMsg "a5" "c3" "read" (encodeId 3 "result3")
    |]
    let r = project msgs
    check "bt: 4 messages" (r.Length = 4)
    check "bt: anchor rewritten" (VibeFs.Kernel.BacktrackCodec.tryParseId (partOutput r.[2]) = Some 1)
    check "bt: new result kept" (VibeFs.Kernel.BacktrackCodec.tryParseId (partOutput r.[3]) = Some 3)
    let allJson : string = Fable.Core.JS.JSON.stringify(r)
    check "bt: result2 gone" (not (allJson.Contains("result2")))

let backtrackNeverVisible () =
    let msgs = [|
        toolResultMsg "a1" "c0" "read" (encodeId 0 "r0")
        backtrackMsg "a2" "bt" 0 "note"
    |]
    let r = project msgs
    check "bt invisible: 1 message" (r.Length = 1)
    check "bt invisible: r0 rewritten to note" (VibeFs.Kernel.BacktrackCodec.tryParseId (partOutput r.[0]) = Some 0)

let userMessagesPreserved () =
    let msgs = [|
        userMsg "u1" "first"
        toolResultMsg "a1" "c0" "read" (encodeId 0 "r0")
        userMsg "u2" "second"
        toolResultMsg "a2" "c1" "read" (encodeId 1 "r1")
        backtrackMsg "a3" "bt" 0 "rewrite"
    |]
    let r = project msgs
    let allJson : string = Fable.Core.JS.JSON.stringify(r)
    check "bt: user first kept" (allJson.Contains("first"))
    check "bt: user second kept" (allJson.Contains("second"))
    check "bt: r1 removed" (not (allJson.Contains("r1")))

let anchorNotFound () =
    let msgs = [|
        toolResultMsg "a1" "c0" "read" (encodeId 0 "r0")
        backtrackMsg "a2" "bt" 99 "note"
    |]
    let r = project msgs
    check "bt no anchor: 1 message" (r.Length = 1)
    check "bt no anchor: r0 intact" (VibeFs.Kernel.BacktrackCodec.tryParseId (partOutput r.[0]) = Some 0)

let visibleIdsCheck () =
    let msgs = [|
        toolResultMsg "a1" "c0" "read" (encodeId 0 "r0")
        toolResultMsg "a2" "c1" "read" (encodeId 5 "r5")
    |]
    let ids = visibleIds msgs
    check "visibleIds: has 0" (ids |> List.contains 0)
    check "visibleIds: has 5" (ids |> List.contains 5)
    check "visibleIds: count 2" (ids.Length = 2)

let syntheticCleaning () =
    let real = toolResultMsg "real-1" "c0" "read" "data"
    let synth = createObj [ "info", box (createObj [ "id", box (rewritePreludeUserPrefix + "v1"); "role", box "user" ]); "parts", box [||] ]
    let msgs = [| synth; real |]
    let cleaned = stripSyntheticMessages msgs
    check "synthetic: stripped to 1" (cleaned.Length = 1)

let run () =
    codec ()
    noBacktrackPassthrough ()
    singleBacktrackRewrite ()
    backtrackNeverVisible ()
    userMessagesPreserved ()
    anchorNotFound ()
    visibleIdsCheck ()
    syntheticCleaning ()
