module Wanxiangshu.Tests.ErrorClassifyTests

open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality

let translateAbortSignalByCode () =
    let e = unbox (createObj [ "code", box "ABORT_ERR" ])

    match translateJsError e with
    | ClientCancellation "AbortSignal" -> ()
    | other -> failwith $"expected ClientCancellation AbortSignal, got %A{other}"

let translateAbortSignalBySignalField () =
    let e = unbox (createObj [ "signal", box "AbortSignal" ])

    match translateJsError e with
    | ClientCancellation "AbortSignal" -> ()
    | other -> failwith $"expected ClientCancellation AbortSignal, got %A{other}"

let translateFileSystemFault () =
    let e =
        unbox (
            createObj
                [ "path", box "/tmp/missing.txt"
                  "errno", box "ENOENT"
                  "message", box "No such file" ]
        )

    match translateJsError e with
    | FileSystemFault(path, errno, msg) ->
        equal "path" "/tmp/missing.txt" path
        equal "errno" "ENOENT" errno
        equal "message" "No such file" msg
    | other -> failwith $"expected FileSystemFault, got %A{other}"

let translateNetworkTransportFailure () =
    let config = unbox (createObj [ "url", box "https://api.example.com/v1" ])

    let e =
        unbox (createObj [ "statusCode", box 404; "config", config; "message", box "Not Found" ])

    match translateJsError e with
    | NetworkTransportFailure(url, status, msg) ->
        equal "url" "https://api.example.com/v1" url
        equal "status" (Some 404) status
        equal "message" "Not Found" msg
    | other -> failwith $"expected NetworkTransportFailure, got %A{other}"

let translateHostProtocolMismatch () =
    let e =
        unbox (
            createObj
                [ "field", box "args.limit"
                  "expected", box "integer"
                  "actual", box "string"
                  "message", box "type mismatch" ]
        )

    match translateJsError e with
    | HostProtocolMismatch(field, expected, actual) ->
        equal "field" "args.limit" field
        equal "expected" "integer" expected
        equal "actual" "string" actual
    | other -> failwith $"expected HostProtocolMismatch, got %A{other}"

let translateUnknownJsError () =
    let e =
        unbox (createObj [ "name", box "CompletelyUnknownError"; "message", box "weird failure" ])

    match translateJsError e with
    | UnknownJsError msg -> equal "message" "weird failure" msg
    | other -> failwith $"expected UnknownJsError, got %A{other}"

let run () =
    translateAbortSignalByCode ()
    translateAbortSignalBySignalField ()
    translateFileSystemFault ()
    translateNetworkTransportFailure ()
    translateHostProtocolMismatch ()
    translateUnknownJsError ()
