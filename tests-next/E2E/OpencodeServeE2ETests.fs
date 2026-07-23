namespace Wanxiangshu.Next.Tests.E2E

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module private NodeFsE2E =
    [<Import("existsSync", "node:fs")>]
    let existsSync (path: string) : bool = jsNative

    [<Import("resolve", "node:path")>]
    let resolve (p: string) : string = jsNative

module private NodeFetchE2E =
    [<Emit("fetch($0).then(r => r.json())")>]
    let fetchJson (url: string) : Task<obj> = jsNative

module private NodeEventLoopE2E =
    [<Import("scheduler", "node:timers/promises")>]
    let private scheduler: obj = jsNative

    let waitForHeartbeatInterval () : Task<unit> =
        unbox (scheduler?wait (100))

module private NodeChildProcE2E =
    [<Import("spawn", "node:child_process")>]
    let spawn (cmd: string) (args: string array) (opts: obj) : obj = jsNative

    [<Emit("process.env")>]
    let processEnv () : obj = jsNative

module OpencodeServeE2ETests =

    [<Fact>]
    let ``Opencode_serve_spawns_and_loads_plugin_e2e`` () =
        withTempDir (fun tempDir ->
            task {
                let pluginPath = NodeFsE2E.resolve "build/next/OpenCode/Plugin.js"
                Assert.True(NodeFsE2E.existsSync pluginPath, sprintf "Plugin file not found at %s" pluginPath)

                let configJson = sprintf """{"plugin":["%s"]}""" (pluginPath.Replace("\\", "/"))

                let envObj =
                    JS.Constructors.Object.assign(
                        createObj [],
                        NodeChildProcE2E.processEnv (),
                        createObj
                            [ "HOME", box tempDir
                              "USERPROFILE", box tempDir
                              "XDG_DATA_HOME", box tempDir
                              "XDG_CONFIG_HOME", box tempDir
                              "XDG_CACHE_HOME", box tempDir
                              "XDG_STATE_HOME", box tempDir
                              "TMPDIR", box tempDir
                              "OPENCODE_DISABLE_AUTOUPDATE", box "1"
                              "OPENCODE_DISABLE_AUTOCOMPACT", box "1"
                              "OPENCODE_DISABLE_MODELS_FETCH", box "1"
                              "OPENCODE_AUTH_CONTENT", box "{}"
                              "OPENCODE_PERMISSION", box """{"*":"allow"}"""
                              "OPENCODE_CONFIG_CONTENT", box configJson ]
                    )

                let opts = createObj [ "cwd", box tempDir; "env", envObj ]
                let child = NodeChildProcE2E.spawn "opencode" [| "serve"; "--port"; "0"; "--hostname"; "127.0.0.1" |] opts

                let mutable stdoutBuf = ""
                let mutable portOpt = None

                let onData =
                    fun (chunk: obj) ->
                        Assert.True(true)
                        let s = unbox<string> (chunk?toString ("utf-8"))
                        stdoutBuf <- stdoutBuf + s
                        let matchIdx = stdoutBuf.IndexOf("opencode server listening on http://127.0.0.1:")

                        if matchIdx >= 0 && portOpt.IsNone then
                            let sub = stdoutBuf.Substring(matchIdx + 46)
                            let endIdx = sub.IndexOfAny([| '\r'; '\n'; ' '; '/'; '\t' |])
                            let portStr = if endIdx > 0 then sub.Substring(0, endIdx) else sub.Trim()

                            match Int32.TryParse(portStr) with
                            | true, port ->
                                portOpt <- Some port
                            | _ -> ()

                child?stdout?on ("data", onData) |> ignore
                child?stderr?on ("data", onData) |> ignore

                let startupDeadline = DateTimeOffset.UtcNow.AddSeconds(15.0)

                while portOpt.IsNone && DateTimeOffset.UtcNow < startupDeadline do
                    Assert.True(true)
                    do! NodeEventLoopE2E.waitForHeartbeatInterval ()

                let port =
                    match portOpt with
                    | Some value -> value
                    | None ->
                        child?kill ("SIGKILL") |> ignore
                        Assert.Fail(sprintf "opencode serve did not publish a port: %s" stdoutBuf)
                        0

                let url = sprintf "http://127.0.0.1:%d/command" port
                let fetchResult = JsTcs<Result<obj, string>>()

                task {
                    try
                        let! value = NodeFetchE2E.fetchJson url
                        fetchResult.TrySetResult(Ok value) |> ignore
                    with error ->
                        fetchResult.TrySetResult(Error error.Message) |> ignore
                }
                |> ignore

                let fetchDeadline = DateTimeOffset.UtcNow.AddSeconds(15.0)

                while not fetchResult.IsCompleted && DateTimeOffset.UtcNow < fetchDeadline do
                    Assert.True(true)
                    do! NodeEventLoopE2E.waitForHeartbeatInterval ()

                if not fetchResult.IsCompleted then
                    child?kill ("SIGKILL") |> ignore
                    Assert.Fail("GET /command did not complete")

                let! result = fetchResult.Task

                let jsonObj =
                    match result with
                    | Ok value -> value
                    | Error error ->
                        child?kill ("SIGKILL") |> ignore
                        Assert.Fail(sprintf "GET /command failed: %s" error)
                        null

                Assert.False(isNull jsonObj, "GET /command returned null")

                let jsonArray: obj array = unbox jsonObj
                let commandNames =
                    jsonArray
                    |> Seq.cast<obj>
                    |> Seq.choose (fun item -> if isNull item || isNull item?name then None else Some(unbox<string> item?name))
                    |> Seq.toList

                Assert.True(List.contains "loop" commandNames, sprintf "Expected 'loop' in commands, got %A" commandNames)

                child?kill ("SIGKILL") |> ignore
            })
