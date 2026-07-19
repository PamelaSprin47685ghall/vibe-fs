module Wanxiangshu.Tests.KernelReviewCoverageTests

open Fable.Core
open Fable.Core.JsInterop
open System
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ToolExecutionStatusModule
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.Config

// ── Kernel.ReviewSession.Types ────────────────────────────────────────────────
let rstEmpty () =
    let e = empty "rs1" 100L
    equal "empty id" "rs1" e.id
    equal "empty state" ReviewState.Inactive e.state
    equal "empty version" 0 e.version
    equal "empty parentId" None e.parentId

let rstWithTask () =
    let e = empty "rs1" 100L
    let once = withTask "t1" e
    equal "withTask set" (Some "t1") once.originalTask
    equal "withTask version 1" 1 once.version
    let same = withTask "t1" once
    equal "withTask same version" once.version same.version
    let diff = withTask "t2" once
    equal "withTask diff version" 2 diff.version
    equal "withTask diff task" (Some "t2") diff.originalTask

let rstWithFeedback () =
    let e = empty "rs1" 100L
    let fb = withFeedback e "good"
    equal "feedback set" (Some "good") fb.lastFeedback
    let same = withFeedback fb "good"
    equal "feedback same version" fb.version same.version
    let diff = withFeedback fb "bad"
    equal "feedback new version" (fb.version + 1) diff.version
    equal "feedback new text" (Some "bad") diff.lastFeedback

let rstAddChild () =
    let e = empty "rs1" 100L
    let c1 = addChild e "c1"
    equal "addChild new" [ "c1" ] c1.childIds
    equal "addChild version" (e.version + 1) c1.version
    let dup = addChild c1 "c1"
    equal "addChild dup" [ "c1" ] dup.childIds
    equal "addChild dup version same" c1.version dup.version

// ── Kernel.Config ─────────────────────────────────────────────────────────────
let cfgStealthBrowserRef () =
    equal "empty → master" "master" (stealthBrowserMcpRef "")
    equal "non-empty passthrough" "feat" (stealthBrowserMcpRef "feat")

let cfgStealthBrowserCommand () =
    let cmd = getStealthBrowserMcpCommand ""
    check "cmd has uvx" (cmd.Contains "uvx")
    check "cmd has 3.13" (cmd.Contains "3.13")
    check "cmd has repo" (cmd.Contains "github.com/vibheksoni/stealth-browser-mcp")

let cfgStealthBrowserLocalConfig () =
    let cfg = getStealthBrowserMcpLocalConfig ""
    equal "localConfig type" "local" cfg.``type``
    let cmd = cfg.command
    check "cmd has uvx" (Array.contains "uvx" cmd)
    check "cmd has python" (Array.contains "python" cmd)

let run () =
    rstEmpty ()
    rstWithTask ()
    rstWithFeedback ()
    rstAddChild ()
    cfgStealthBrowserRef ()
    cfgStealthBrowserCommand ()
    cfgStealthBrowserLocalConfig ()
