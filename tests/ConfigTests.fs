module Wanxiangshu.Tests.ConfigTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Config
open Microsoft.FSharp.Collections

let stealthBrowserMcpRefEmptyReturnsMaster () =
    equal "empty env → master" "master" (stealthBrowserMcpRef "")

let stealthBrowserMcpRefNonEmptyPassthrough () =
    equal "non-empty env passthrough" "feature-x" (stealthBrowserMcpRef "feature-x")

let getStealthBrowserMcpCommandContainsAllPartsEmptyRef () =
    let cmd = getStealthBrowserMcpCommand ""
    check "contains uvx" (cmd.Contains "uvx")
    check "contains --python" (cmd.Contains "--python")
    check "contains 3.13" (cmd.Contains "3.13")
    check "contains repo" (cmd.Contains "https://github.com/vibheksoni/stealth-browser-mcp.git")
    check "contains master" (cmd.Contains "master")

let getStealthBrowserMcpCommandContainsAllPartsCustomRef () =
    let cmd = getStealthBrowserMcpCommand "my-branch"
    check "contains uvx" (cmd.Contains "uvx")
    check "contains --python" (cmd.Contains "--python")
    check "contains 3.13" (cmd.Contains "3.13")
    check "contains repo" (cmd.Contains "https://github.com/vibheksoni/stealth-browser-mcp.git")
    check "contains custom ref" (cmd.Contains "my-branch")

let getStealthBrowserMcpLocalConfigTypeLocal () =
    let cfg = getStealthBrowserMcpLocalConfig ""
    equal "type is local" "local" cfg.``type``

let getStealthBrowserMcpLocalConfigCommandElements () =
    let cfg = getStealthBrowserMcpLocalConfig ""
    let cmd = cfg.command
    check "command has uvx" (Array.contains "uvx" cmd)
    check "command has --python" (Array.contains "--python" cmd)
    check "command has 3.13" (Array.contains "3.13" cmd)
    check "command has --from" (Array.contains "--from" cmd)
    check "command has python" (Array.contains "python" cmd)
    check "command has -m" (Array.contains "-m" cmd)
    check "command has server" (Array.contains "server" cmd)

let sembleMcpRefEmptyReturnsMaster () =
    equal "empty env -> main" "main" (sembleMcpRef "")

let sembleMcpRefNonEmptyPassthrough () =
    equal "non-empty env passthrough" "feature-x" (sembleMcpRef "feature-x")

let getSembleMcpCommandElements () =
    let cmd = getSembleMcpCommand ""
    equal "command is uvx" "uvx" cmd.command
    check "args has --from" (Array.contains "--from" cmd.args)
    let reqArg = cmd.args |> Array.find (fun a -> a.Contains "semble[mcp]")
    check "requirement carries mcp extra" (reqArg.Contains "[mcp]")
    check "requirement carries git url" (reqArg.Contains "https://github.com/MinishLab/semble.git")
    check "requirement carries main" (reqArg.Contains "main")
    check "args has no python" (not (Array.contains "python" cmd.args))
    check "args has semble entrypoint" (Array.contains "semble" cmd.args)
    check "args has no --extra" (not (Array.contains "--extra" cmd.args))

let getSembleMcpCommandCustomRef () =
    let cmd = getSembleMcpCommand "dev-branch"
    check "args contains custom ref" (cmd.args |> Array.exists (fun a -> a.Contains "dev-branch"))

let run () =
    stealthBrowserMcpRefEmptyReturnsMaster ()
    stealthBrowserMcpRefNonEmptyPassthrough ()
    getStealthBrowserMcpCommandContainsAllPartsEmptyRef ()
    getStealthBrowserMcpCommandContainsAllPartsCustomRef ()
    getStealthBrowserMcpLocalConfigTypeLocal ()
    getStealthBrowserMcpLocalConfigCommandElements ()
    sembleMcpRefEmptyReturnsMaster ()
    sembleMcpRefNonEmptyPassthrough ()
    getSembleMcpCommandElements ()
    getSembleMcpCommandCustomRef ()
