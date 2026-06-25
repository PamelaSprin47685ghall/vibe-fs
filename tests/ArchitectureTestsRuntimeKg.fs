module VibeFs.Tests.ArchitectureTestsRuntimeKg

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.ArchitectureTestsSupport

let knowledgeGraphRuntimeUsesWorkflow () =
    let opencode = requireFile "src/Opencode/KnowledgeGraphRuntime.fs" |> nonCommentCode
    check "arch: Opencode KnowledgeGraphRuntime opens KnowledgeGraphWorkflow"
        (opencode.Contains "KnowledgeGraphWorkflow")
    check "arch: Opencode KnowledgeGraphRuntime uses trackBackgroundJob"
        (opencode.Contains "trackBackgroundJob")
    check "arch: Opencode KnowledgeGraphRuntime uses recordLaunchResult"
        (opencode.Contains "recordLaunchResult")
    check "arch: Opencode KnowledgeGraphRuntime no local ResizeArray backgroundJobs"
        (not (opencode.Contains "let backgroundJobs = ResizeArray"))
    let mux = requireFile "src/Mux/KnowledgeGraphTools.fs" |> nonCommentCode
    check "arch: Mux KnowledgeGraphTools opens KnowledgeGraphWorkflow"
        (mux.Contains "KnowledgeGraphWorkflow")
    check "arch: Mux KnowledgeGraphTools uses trackBackgroundJob"
        (mux.Contains "trackBackgroundJob")
    check "arch: Mux KnowledgeGraphTools uses recordLaunchResult"
        (mux.Contains "recordLaunchResult")
    check "arch: Mux KnowledgeGraphTools no local ResizeArray backgroundJobs"
        (not (mux.Contains "let backgroundJobs = ResizeArray"))

let knowledgeGraphBookkeeperLaunchInShell () =
    let launch = requireFile "src/Shell/KnowledgeGraphBookkeeperLaunch.fs" |> nonCommentCode
    check "arch: Shell KnowledgeGraphBookkeeperLaunch defines queueBackgroundLaunch"
        (launch.Contains "let queueBackgroundLaunch")
    check "arch: Shell KnowledgeGraphBookkeeperLaunch defines launchBackgroundSession"
        (launch.Contains "let launchBackgroundSession")
    check "arch: Shell KnowledgeGraphBookkeeperLaunch defines queueMuxBackgroundLaunch"
        (launch.Contains "let queueMuxBackgroundLaunch")
    let opencodeIo = requireFile "src/Opencode/KnowledgeGraphRuntimeIO.fs" |> nonCommentCode
    check "arch: Opencode KnowledgeGraphRuntimeIO no queueBackgroundLaunch"
        (not (opencodeIo.Contains "let queueBackgroundLaunch"))
    let opencode = requireFile "src/Opencode/KnowledgeGraphRuntime.fs" |> nonCommentCode
    check "arch: Opencode KnowledgeGraphRuntime opens KnowledgeGraphBookkeeperLaunch"
        (opencode.Contains "KnowledgeGraphBookkeeperLaunch")
    check "arch: Opencode KnowledgeGraphRuntime calls queueBackgroundLaunch"
        (opencode.Contains "queueBackgroundLaunch")
    let mux = requireFile "src/Mux/KnowledgeGraphTools.fs" |> nonCommentCode
    check "arch: Mux KnowledgeGraphTools calls queueMuxBackgroundLaunch"
        (mux.Contains "queueMuxBackgroundLaunch")
    check "arch: Mux KnowledgeGraphTools no inline delegate bookkeeper trackBackgroundJob block"
        (not (mux.Contains "delegateToSubAgent deps cfg \"bookkeeper\""))
    for path in [| "src/Opencode/KnowledgeGraphRuntime.fs"; "src/Mux/KnowledgeGraphTools.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " no TestObservation type")
            (not (code.Contains "TestObservation"))
        check ("arch: " + path + " no member Observation")
            (not (code.Contains "member _.Observation"))
        check ("arch: " + path + " exposes CreateTestPorts")
            (code.Contains "CreateTestPorts")

let knowledgeGraphRuntimeNoLocalLaunchIfDue () =
    for path in [| "src/Opencode/KnowledgeGraphRuntime.fs"; "src/Mux/KnowledgeGraphTools.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " uses runMaintenanceIfDue")
            (code.Contains "runMaintenanceIfDue")
        check ("arch: " + path + " no local launchIfDue")
            (not (code.Contains "let launchIfDue"))

let knowledgeGraphSessionMessagesNotInRuntimeIO () =
    let opencodeIo = requireFile "src/Opencode/KnowledgeGraphRuntimeIO.fs" |> nonCommentCode
    let sessionMessages = requireFile "src/Opencode/KnowledgeGraphSessionMessages.fs" |> nonCommentCode
    check "arch: KnowledgeGraphSessionMessages defines fetchSessionMessageArray"
        (sessionMessages.Contains "let fetchSessionMessageArray")
    check "arch: KnowledgeGraphSessionMessages defines loadSessionMessages"
        (sessionMessages.Contains "let loadSessionMessages")
    check "arch: KnowledgeGraphSessionMessages defines tryResolveJobContext"
        (sessionMessages.Contains "let tryResolveJobContext")
    check "arch: Opencode KnowledgeGraphRuntimeIO no fetchSessionMessageArray"
        (not (opencodeIo.Contains "fetchSessionMessageArray"))

let knowledgeGraphRuntimeNoTestDrainMembers () =
    let opencode = requireFile "src/Opencode/KnowledgeGraphRuntime.fs" |> nonCommentCode
    let mux = requireFile "src/Mux/KnowledgeGraphTools.fs" |> nonCommentCode
    check "arch: Opencode KnowledgeGraphRuntime no takeBookkeeperLaunchesForTesting"
        (not (opencode.Contains "takeBookkeeperLaunchesForTesting"))
    check "arch: Opencode KnowledgeGraphRuntime no clearBackgroundJobsForTesting"
        (not (opencode.Contains "clearBackgroundJobsForTesting"))
    check "arch: Mux KnowledgeGraphTools no takeBookkeeperLaunchesForTesting"
        (not (mux.Contains "takeBookkeeperLaunchesForTesting"))
    check "arch: Mux KnowledgeGraphTools no clearBackgroundJobsForTesting"
        (not (mux.Contains "clearBackgroundJobsForTesting"))

let knowledgeGraphRuntimeNoSwapStateMembers () =
    let opencode = requireFile "src/Opencode/KnowledgeGraphRuntime.fs" |> nonCommentCode
    let mux = requireFile "src/Mux/KnowledgeGraphTools.fs" |> nonCommentCode
    check "arch: Opencode KnowledgeGraphRuntime no swapStateForTesting"
        (not (opencode.Contains "swapStateForTesting"))
    check "arch: Opencode KnowledgeGraphRuntime no restoreStateForTesting"
        (not (opencode.Contains "restoreStateForTesting"))
    check "arch: Mux KnowledgeGraphTools no swapStateForTesting"
        (not (mux.Contains "swapStateForTesting"))
    check "arch: Mux KnowledgeGraphTools no restoreStateForTesting"
        (not (mux.Contains "restoreStateForTesting"))