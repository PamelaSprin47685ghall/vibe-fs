module Wanxiangshu.Tests.OpencodeSessionSpawnCodecTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell.OpencodeSessionSpawnCodec

let decodeOkId () =
    let createResult = createObj [ "data", createObj [ "id", box "  child-abc  " ] ]
    match decodeChildSessionIdFromCreateResult createResult with
    | Ok id -> check "child id trimmed" (id = "child-abc")
    | Error _ -> check "child id ok" false

let decodeNullCreateResult () =
    match decodeChildSessionIdFromCreateResult null with
    | Error (InvalidIntent ("session", "id", _)) -> check "null createResult" true
    | _ -> check "null createResult invalid intent" false

let decodeMissingId () =
    let noData = createObj []
    match decodeChildSessionIdFromCreateResult noData with
    | Error (InvalidIntent ("session", "id", _)) -> check "missing data" true
    | _ -> check "missing data invalid intent" false

    let emptyId = createObj [ "data", createObj [ "id", box "" ] ]
    match decodeChildSessionIdFromCreateResult emptyId with
    | Error (InvalidIntent ("session", "id", _)) -> check "empty id" true
    | _ -> check "empty id invalid intent" false

    let whitespaceId = createObj [ "data", createObj [ "id", box "   " ] ]
    match decodeChildSessionIdFromCreateResult whitespaceId with
    | Error (InvalidIntent ("session", "id", _)) -> check "whitespace id" true
    | _ -> check "whitespace id invalid intent" false

let run () =
    decodeOkId ()
    decodeNullCreateResult ()
    decodeMissingId ()