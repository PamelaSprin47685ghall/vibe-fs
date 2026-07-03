module Wanxiangshu.Tests.E2eHarnessContractTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

/// E2e harness getMessages must hit /session/:id/message, not /api/session/:id/message.
/// The /api/session prefix path returns empty for GET; only /session/ returns the
/// message array. Regression guard: if the path is ever rewritten to /api/session
/// the history-echo e2e silently passes an empty list and the bug ships.
let getMessagesUsesSessionPrefix () =
    let code = requireFile "e2e/harness.js"
    let idx = code.IndexOf("async getMessages")
    check "e2e.harness.getMessages exists" (idx >= 0)
    if idx >= 0 then
        let body = code.Substring(idx, min 250 (code.Length - idx))
        check "e2e.harness.getMessages GETs /session/${sessionID}/message"
            (body.Contains("request('GET', `/session/${sessionID}/message`"))
        check "e2e.harness.getMessages must not prefix /api/session/"
            (not (body.Contains("/api/session/")))

/// E2e harness PLUGIN_JS must resolve to build/src/Opencode/Plugin.js from both
/// repo-root runs (__dirname=e2e) and build-dir runs (__dirname=build/e2e). The
/// build pipeline copies e2e/*.js into build/e2e/, so a rigid `..` would point
/// at build/ and yield build/build/src/Opencode/Plugin.js. The fallback to
/// `../..` when the first guess misses is the only thing that keeps the e2e
/// plugin load working after `npm run build-and-test`.
let pluginJsResolvesWithParentFallback () =
    let code = requireFile "e2e/harness.js"
    check "e2e.harness.PLUGIN_JS targets build/src/Opencode/Plugin.js"
        (code.Contains("build/src/Opencode/Plugin.js"))
    check "e2e.harness.PLUGIN_JS has existsSync fallback to parent"
        (code.Contains("existsSync") && code.Contains("../.."))
    check "e2e.harness.WANXIANG_ROOT is mutable (let not const)"
        (code.Contains("let WANXIANG_ROOT"))
