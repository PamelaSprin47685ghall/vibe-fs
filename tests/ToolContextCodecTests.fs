module Wanxiangshu.Tests.ToolContextCodecTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell.ToolContextCodec

let decodeMuxConfigMissingWorkspaceId () =
    let config = createObj []
    match decodeMuxConfig config with
    | Error (InvalidIntent ("mux", "workspaceId", "required")) -> check "mux missing workspaceId" true
    | _ -> check "mux missing workspaceId" false

let decodeMuxConfigEmptyWorkspaceId () =
    let config = createObj [ "workspaceId", box "" ]
    match decodeMuxConfig config with
    | Error (InvalidIntent ("mux", "workspaceId", "required")) -> check "mux empty workspaceId" true
    | _ -> check "mux empty workspaceId" false

let decodeMuxConfigSessionIdFromSessionID () =
    let config = createObj [ "workspaceId", box "ws-1"; "sessionID", box "sess-abc" ]
    match decodeMuxConfig config with
    | Ok ctx -> check "mux sessionID" (Id.sessionIdValue ctx.SessionId = "sess-abc")
    | Error _ -> check "mux sessionID" false

let decodeMuxConfigSessionIdFromSessionIdCamel () =
    let config = createObj [ "workspaceId", box "ws-1"; "sessionId", box "sess-camel" ]
    match decodeMuxConfig config with
    | Ok ctx -> check "mux sessionId" (Id.sessionIdValue ctx.SessionId = "sess-camel")
    | Error _ -> check "mux sessionId" false

let decodeMuxConfigSessionIdFromSessionSnake () =
    let config = createObj [ "workspaceId", box "ws-1"; "session_id", box "sess-snake" ]
    match decodeMuxConfig config with
    | Ok ctx -> check "mux session_id" (Id.sessionIdValue ctx.SessionId = "sess-snake")
    | Error _ -> check "mux session_id" false

let decodeMuxConfigSessionIdPrefersSessionID () =
    let config =
        createObj [
            "workspaceId", box "ws-1"
            "sessionID", box "first"
            "sessionId", box "second"
            "session_id", box "third"
        ]
    match decodeMuxConfig config with
    | Ok ctx -> check "mux session key priority" (Id.sessionIdValue ctx.SessionId = "first")
    | Error _ -> check "mux session key priority" false

let decodeMuxConfigNoSession () =
    let config = createObj [ "workspaceId", box "ws-1" ]
    match decodeMuxConfig config with
    | Ok ctx ->
        check "mux no session empty" (Id.sessionIdValue ctx.SessionId = "")
        check "mux workspaceId" (ctx.WorkspaceId = Some (Id.workspaceIdQuick "ws-1"))
    | Error _ -> check "mux no session" false

let decodeMuxConfigLenientMissingWorkspaceId () =
    let config = createObj [ "cwd", box " /tmp/len "; "sessionId", box "s-len" ]
    let ctx = decodeMuxConfigLenient config
    check "lenient no workspaceId" (ctx.WorkspaceId = None)
    check "lenient cwd trim" (ctx.Directory = "/tmp/len")
    check "lenient session" (Id.sessionIdValue ctx.SessionId = "s-len")

let decodeMuxConfigOkDirectory () =
    let config =
        createObj [
            "workspaceId", box "ws-1"
            "directory", box "/proj"
            "sessionID", box "s1"
        ]
    match decodeMuxConfig config with
    | Ok ctx ->
        check "mux directory" (ctx.Directory = "/proj")
        check "mux ok session" (Id.sessionIdValue ctx.SessionId = "s1")
    | Error _ -> check "mux ok directory" false

let run () =
    decodeMuxConfigLenientMissingWorkspaceId ()
    decodeMuxConfigMissingWorkspaceId ()
    decodeMuxConfigEmptyWorkspaceId ()
    decodeMuxConfigSessionIdFromSessionID ()
    decodeMuxConfigSessionIdFromSessionIdCamel ()
    decodeMuxConfigSessionIdFromSessionSnake ()
    decodeMuxConfigSessionIdPrefersSessionID ()
    decodeMuxConfigNoSession ()
    decodeMuxConfigOkDirectory ()