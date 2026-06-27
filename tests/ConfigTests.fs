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

let run () =
    stealthBrowserMcpRefEmptyReturnsMaster ()
    stealthBrowserMcpRefNonEmptyPassthrough ()
    getStealthBrowserMcpCommandContainsAllPartsEmptyRef ()
    getStealthBrowserMcpCommandContainsAllPartsCustomRef ()
    getStealthBrowserMcpLocalConfigTypeLocal ()
    getStealthBrowserMcpLocalConfigCommandElements ()
