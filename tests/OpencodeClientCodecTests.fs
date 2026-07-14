module Wanxiangshu.Tests.OpencodeClientCodecTests

open Fable.Core.JsInterop
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Tests.Assert

/// getConfigApiFromClient must mirror getSessionApiFromClient's shape:
/// Ok when the sub-API exists on the client, Error(InvalidIntent) otherwise.
let getConfigApiFromClientFindsConfigSubApi () =
    let configApi = createObj [ "get", box (fun () -> ()) ]
    let client = createObj [ "config", box configApi ]

    match getConfigApiFromClient client with
    | Ok api -> check "returns the config sub-object" (obj.ReferenceEquals(api, configApi))
    | Error _ -> check "expected Ok" false

let getConfigApiFromClientMissingReturnsError () =
    let client = createObj [ "session", box (createObj []) ]

    match getConfigApiFromClient client with
    | Error _ -> check "expected Error when config missing" true
    | Ok _ -> check "expected Error" false

let run () =
    getConfigApiFromClientFindsConfigSubApi ()
    getConfigApiFromClientMissingReturnsError ()
