module Wanxiangshu.Tests.ArchitectureTestsFallback

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

let fallbackFiles =
    [| "src/Kernel/FallbackKernel/Types.fs"
       "src/Kernel/FallbackKernel/Decision.fs"
       "src/Kernel/FallbackKernel/Recovery.fs"
       "src/Kernel/FallbackKernel/StateMachine.fs"
       "src/Shell/FallbackConfigCodec.fs"
       "src/Shell/FallbackRuntimeState.fs"
       "src/Shell/FallbackMessageCodec.fs"
       "src/Shell/FallbackEventBridge.fs"
       "src/Shell/FallbackRecoveryWait.fs"
       "src/Opencode/FallbackHooks.fs"
       "src/Opencode/FallbackConfigLoader.fs"
       "src/Mux/FallbackHooks.fs"
       "src/Omp/FallbackHooks.fs" |]

let zeroTimer () =
    for path in fallbackFiles do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " no setTimeout") (not (code.Contains "setTimeout"))
        check ("arch: " + path + " no setInterval") (not (code.Contains "setInterval"))
        check ("arch: " + path + " no Date.now") (not (code.Contains "Date.now"))

let kernelPurity () =
    for f in fsFiles "src/Kernel/FallbackKernel" do
        let path = "src/Kernel/FallbackKernel/" + f
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " no Dyn") (not (code.Contains "Dyn."))
        check ("arch: " + path + " no Shell ref") (not (code.Contains "Wanxiangshu.Shell"))
        check ("arch: " + path + " no Node ref") (not (code.Contains "node:"))

let ompFallbackIsolation () =
    for f in fsFiles "src/Omp" do
        if f.StartsWith "Fallback" then
            let path = "src/Omp/" + f
            let code = requireFile path |> nonCommentCode
            check ("arch: " + path + " no Opencode ref") (not (code.Contains "Wanxiangshu.Opencode"))
            check ("arch: " + path + " no Mux ref") (not (code.Contains "Wanxiangshu.Mux"))

let configSsot () =
    let codec = requireFile "src/Shell/FallbackConfigCodec.fs" |> nonCommentCode
    check "arch: FallbackConfigCodec defines extractFallbackConfig" (codec.Contains "let extractFallbackConfig")
    check "arch: FallbackConfigCodec defines loadFallbackConfig" (codec.Contains "let loadFallbackConfig")

let consumerFiles =
    [| "src/Shell/FallbackMessageCodec.fs"; "src/Shell/NudgeRuntimeTypes.fs" |]

let fallbackInjectionStateMustReplayHistory () =
    for path in consumerFiles do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " forbids text = \"​\" sniffing") (not (code.Contains "text = \"​\""))
        check ("arch: " + path + " forbids t = \"​\" sniffing") (not (code.Contains "t = \"​\""))

    let bridge = requireFile "src/Shell/FallbackEventBridge.fs" |> nonCommentCode
    check "arch: FallbackEventBridge.handleEvent takes workspaceRoot" (bridge.Contains "workspaceRoot: string")

    let runtime = requireFile "src/Shell/FallbackRuntimeState.fs" |> nonCommentCode
    check "arch: FallbackRuntimeState exposes GetInjectedModel" (runtime.Contains "GetInjectedModel")
    check "arch: FallbackRuntimeState exposes IsInjectedSince" (runtime.Contains "IsInjectedSince")

    let types = requireFile "src/Kernel/EventLog/Types.fs" |> nonCommentCode

    check
        "arch: Kernel EventLog declares fallback_continue_injected kind"
        (types.Contains "eventKindFallbackContinueInjected")
