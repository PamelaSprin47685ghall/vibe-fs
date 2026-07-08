module Wanxiangshu.Tests.HostMessagePartCodecTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.HostMessagePartCodec

let getMessagePartsEmptyWhenNull () =
    let parts = getMessageParts null
    check "parts null msg" (parts.Length = 0)

let getMessagePartsEmptyWhenNoParts () =
    let msg = createObj [ "role", box "assistant" ]
    let parts = getMessageParts msg
    check "parts missing" (parts.Length = 0)

let extractTextPart () =
    let parts =
        [| createObj [ "type", box "text"; "text", box "line one" ]
           createObj [ "type", box "text"; "text", box "" ] |]

    let lines = extractTextLinesFromParts parts |> Seq.toList
    equal "text part lines count" 1 lines.Length
    check "text part content" (lines.[0] = "line one")

let decodeDynamicToolReadOutputString () =
    let part = createObj [ "type", box "dynamic-tool"; "output", box "read file body" ]

    match decodeDynamicToolReadOutput part with
    | Some s -> check "dynamic-tool string output" (s = "read file body")
    | None -> check "dynamic-tool string output" false

let decodeDynamicToolReadOutputContentObject () =
    let part =
        createObj
            [ "type", box "dynamic-tool"
              "output", box (createObj [ "content", box "nested content" ]) ]

    match decodeDynamicToolReadOutput part with
    | Some s -> check "dynamic-tool object output" (s = "nested content")
    | None -> check "dynamic-tool object output" false

let decodeDynamicToolReadOutputWrongType () =
    let part = createObj [ "type", box "text"; "text", box "x" ]

    match decodeDynamicToolReadOutput part with
    | None -> check "dynamic-tool wrong type" true
    | Some _ -> check "dynamic-tool wrong type" false

let run () =
    getMessagePartsEmptyWhenNull ()
    getMessagePartsEmptyWhenNoParts ()
    extractTextPart ()
    decodeDynamicToolReadOutputString ()
    decodeDynamicToolReadOutputContentObject ()
    decodeDynamicToolReadOutputWrongType ()
