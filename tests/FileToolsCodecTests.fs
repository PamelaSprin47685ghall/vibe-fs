module VibeFs.Tests.FileToolsCodecTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Domain
open VibeFs.Shell.Dyn
open VibeFs.Shell.FileToolsCodec

let decodeReadMissingPath () =
    let args = createObj [ "offset", box 1; "limit", box 10 ]
    match decodeReadArgs args with
    | Error (InvalidIntent ("read", "path", _)) -> check "read missing path" true
    | _ -> check "read missing path" false

let decodeReadOkWithOptionalPaging () =
    let args =
        createObj [
            "path", box "/tmp/a.txt"
            "offset", box 5
            "limit", box 20
        ]
    match decodeReadArgs args with
    | Ok ra ->
        check "read ok path" (ra.Path = "/tmp/a.txt")
        check "read ok offset" (ra.Offset = Some 5)
        check "read ok limit" (ra.Limit = Some 20)
    | Error _ -> check "read ok with offset limit" false

let decodeReadOkWithoutPaging () =
    let args = createObj [ "path", box "README.md" ]
    match decodeReadArgs args with
    | Ok ra ->
        check "read ok path only" (ra.Path = "README.md")
        check "read ok offset absent" (ra.Offset = None)
        check "read ok limit absent" (ra.Limit = None)
    | Error _ -> check "read ok without paging" false

let decodeWriteMissingFilePath () =
    let args = createObj [ "content", box "x" ]
    match decodeWriteArgs args with
    | Error (InvalidIntent ("write", "file_path", "missing required parameter")) ->
        check "write missing file_path" true
    | _ -> check "write missing file_path" false

let decodeWriteEmptyFilePath () =
    let args = createObj [ "file_path", box "   "; "content", box "body" ]
    match decodeWriteArgs args with
    | Error (InvalidIntent ("write", "file_path", "must not be empty")) ->
        check "write empty file_path" true
    | _ -> check "write empty file_path" false

let decodeWriteOk () =
    let args = createObj [ "file_path", box "out.txt"; "content", box "hello" ]
    match decodeWriteArgs args with
    | Ok wa ->
        check "write ok file_path" (wa.FilePath = "out.txt")
        check "write ok content" (wa.Content = "hello")
    | Error _ -> check "write ok" false

let readArgsForHostOmitsExtraKeys () =
    let junkKey = "__junkHostField"
    let args =
        createObj [
            "path", box "/tmp/read.txt"
            "offset", box 3
            "limit", box 9
            junkKey, box "should-not-leak"
        ]
    match decodeReadArgs args with
    | Ok ra ->
        let hostArgs = readArgsForHost ra
        let json = Fable.Core.JS.JSON.stringify hostArgs
        check "readArgsForHost json has path" (json.Contains "\"path\"")
        check "readArgsForHost json has offset" (json.Contains "\"offset\"")
        check "readArgsForHost json omits junk key" (not (json.Contains junkKey))
        check "readArgsForHost path value" (str hostArgs "path" = "/tmp/read.txt")
        check "readArgsForHost offset value" (unbox<int> (get hostArgs "offset") = 3)
    | Error _ -> check "readArgsForHost decode read with junk" false

let run () =
    decodeReadMissingPath ()
    decodeReadOkWithOptionalPaging ()
    decodeReadOkWithoutPaging ()
    readArgsForHostOmitsExtraKeys ()
    decodeWriteMissingFilePath ()
    decodeWriteEmptyFilePath ()
    decodeWriteOk ()