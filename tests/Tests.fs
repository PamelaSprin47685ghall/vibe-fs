module Wanxiangshu.Tests.Tests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ReviewTests
open Wanxiangshu.Tests.AgentTests
open Wanxiangshu.Tests.AgentNudgeSpecs
open Wanxiangshu.Tests.KernelTests
open Wanxiangshu.Tests.KernelPromptSpecs
open Wanxiangshu.Tests.FuzzyTests
open Wanxiangshu.Tests.ShellTests
open Wanxiangshu.Tests.DynTests
open Wanxiangshu.Tests.DelegateTests
open Wanxiangshu.Tests.DelegateToolsCodecTests
open Wanxiangshu.Tests.ResolveAiSettingsTests
open Wanxiangshu.Tests.IntegrationPluginTests
open Wanxiangshu.Tests.IntegrationEventTests
open Wanxiangshu.Tests.IntegrationToolSpecCatalog
open Wanxiangshu.Tests.IntegrationOpencodeReviewSpecs
open Wanxiangshu.Tests.IntegrationChatTests
open Wanxiangshu.Tests.WorkBacklogTests
open Wanxiangshu.Tests.MethodologyTests



open Wanxiangshu.Tests.TitleFetchGuardTests
open Wanxiangshu.Tests.TestsTestBody
open Wanxiangshu.Tests.TestsArchitectureRegistry
open Wanxiangshu.Tests.TestsEntriesCore
open Wanxiangshu.Tests.TestsEntriesWanxiangzhen
open Wanxiangshu.Tests.TestsEntriesCodec
open Wanxiangshu.Tests.TestsEntriesOmp
open Wanxiangshu.Tests.ReviewReplaySyncTests
open Wanxiangshu.Tests.CapsSynthCommonTests
open Wanxiangshu.Tests.CapsFileCacheTests
open Wanxiangshu.Tests.SubagentPromptBuildTests
open Wanxiangshu.Tests.SubagentSpawnTests
open Wanxiangshu.Tests.WebToolsCodecTests
open Wanxiangshu.Tests.ReviewToolsCodecTests

open Wanxiangshu.Tests.ExecutorToolsCodecTests
open Wanxiangshu.Tests.ExecutorTests
open Wanxiangshu.Tests.ToolArgsDecodeTests
open Wanxiangshu.Tests.ToolResultWireTests
open Wanxiangshu.Tests.SubagentToolExecuteTests
open Wanxiangshu.Tests.FileToolsCodecTests
open Wanxiangshu.Tests.FuzzyToolsCodecTests
open Wanxiangshu.Tests.WorkBacklogToolsCodecTests
open Wanxiangshu.Tests.PatchToolsCodecTests
open Wanxiangshu.Tests.HostMessagePartCodecTests
open Wanxiangshu.Tests.MessagingPartCodecTests
open Wanxiangshu.Tests.ToolContextCodecTests
open Wanxiangshu.Tests.OpencodeContextCodecTests
open Wanxiangshu.Tests.OpencodeSessionPromptCodecTests
open Wanxiangshu.Tests.OpencodeSessionSpawnCodecTests
open Wanxiangshu.Tests.SessionIoPromptBodyTests
open Wanxiangshu.Tests.OpencodeAgentConfigCodecTests
open Wanxiangshu.Tests.OpencodeSessionEventCodecTests
open Wanxiangshu.Tests.MuxAiSettingsCodecTests
open Wanxiangshu.Tests.MuxAiSettingsIntegrationTests
open Wanxiangshu.Tests.AgentConfigApplyTests



open Wanxiangshu.Tests.SessionExecutorScopeTests
open Wanxiangshu.Tests.OmpKernelTests
open Wanxiangshu.Tests.OmpSessionToolsTests
open Wanxiangshu.Tests.OmpWebFetchTests
open Wanxiangshu.Tests.OmpCapsTests
open Wanxiangshu.Tests.OmpFuzzyTests
open Wanxiangshu.Tests.OmpPluginTests
open Wanxiangshu.Tests.OmpPluginTestsAgentEnd
open Wanxiangshu.Tests.OmpReviewTests
open Wanxiangshu.Tests.OmpHelpersTests
open Wanxiangshu.Tests.OmpRunnerTests
open Wanxiangshu.Tests.OmpContextTransformTests
open Wanxiangshu.Tests.OmpChildSessionTests
open Wanxiangshu.Tests.OmpAgentConfigTests
open Wanxiangshu.Tests.OmpHookExecuteTests

open Wanxiangshu.Tests.OmpSessionLifecycleTests
open Wanxiangshu.Tests.OmpPluginCoreTests
open Wanxiangshu.Tests.OmpTitleFetchGuardTests
open Wanxiangshu.Tests.OmpMagicTodoTests
open Wanxiangshu.Tests.OmpPluginCoreIntegrationTests
open Wanxiangshu.Tests.EventDrivenHarnessDemo
open Wanxiangshu.Tests.SubagentIoTests
open Wanxiangshu.Tests.E2eHarnessContractTests
open Wanxiangshu.Tests.ToolCatalogClassificationTests
open Wanxiangshu.Tests.ToolOutputInfoTests
open Wanxiangshu.Tests.KernelHelpersTests

open Wanxiangshu.Tests.ReviewPromptsFormatTests
open Wanxiangshu.Omp
open Wanxiangshu.Omp.Plugin
open Wanxiangshu.Omp.PluginCore
open Wanxiangshu.Shell

[<Import("appendFileSync", "node:fs")>]
let private appendFile (path: string) (content: string) (encoding: string) : unit = jsNative

[<Import("mkdirSync", "node:fs")>]
let private mkdirSync (path: string) (opts: obj) : unit = jsNative

[<Emit("process.env.VIBE_FS_TEST_VERBOSE")>]
let private envVerbose () : string = jsNative

[<Emit("process.argv")>]
let private cliArgv () : obj = jsNative

let private verboseEnabledFromArgs () : bool =
    let envVal = envVerbose ()
    let envHit = not (isNull envVal) && envVal = "1"
    let argvObj = cliArgv ()

    let cliHit =
        try
            let arr: string[] = unbox argvObj
            arr |> Array.exists (fun a -> a = "--verbose")
        with _ ->
            false

    envHit || cliHit

let private initVerboseLog () : unit =
    if verboseEnabledFromArgs () then
        let ts = System.DateTime.Now.ToString("yyyyMMdd-HHmmss")
        let logDir = "tests/logs"
        let logPath = sprintf "%s/%s.verbose.log" logDir ts

        try
            mkdirSync logDir (createObj [ "recursive", box true ])
        with _ ->
            ()

        let header =
            sprintf
                "# vibe-fs verbose test log\n# timestamp: %s\n# switch: %s\n"
                ts
                (if (envVerbose ()) = "1" then
                     "VIBE_FS_TEST_VERBOSE=1"
                 else
                     "--verbose")

        appendFile logPath header "utf8"
        Assert.setVerbose (Some logPath)

let private integrationToolFlatTests: (string * TestBody) list =
    integrationToolSpecs ()
    |> List.map (fun (shortName, spec) -> "IntegrationTool." + shortName, Async spec)

let private tests: (string * TestBody) list =
    coreTestEntries ()
    @ wanxiangzhenTestEntries ()
    @ (architectureTestEntries ())
    @ codecTestEntries ()
    @ ompTestEntries ()
    @ integrationToolFlatTests

let private matchesSelector (selectors: string array) (label: string) =
    selectors.Length = 0
    || selectors
       |> Array.exists (fun selector ->
           let trimmed = selector.Trim()
           trimmed.Length > 0 && label.StartsWith trimmed)

let private selectedTests (selectors: string array) =
    tests |> List.filter (fun (label, _) -> matchesSelector selectors label)

let runAll (args: string array) : JS.Promise<int> =
    promise {
        clearFailuresForRun ()
        let isSilent = args |> Array.contains "--silent"
        Assert.setSilent isSilent
        let selectors = args |> Array.filter (fun a -> a <> "--verbose" && a <> "-v" && a <> "--silent")
        initVerboseLog ()
        PluginCore.reviewStore.clearReviewSessions ()
        RunnerBackground.clearRunnerLogsForTest ExecutorTools.ompScope
        let runnableTests = selectedTests selectors

        if List.isEmpty runnableTests then
            printfn "No tests matched selectors: %A" args
            return 1
        else
            let isIntegrationSuiteRun (label: string) =
                (label.StartsWith "Integration" && label.EndsWith ".run")
                || (label = "OmpExecutorToolsTests.run")
                || (label = "ExecutorTests.run")

            for (label, body) in runnableTests do
                match body with
                | Sync f -> timed label f
                | Async f ->
                    if isIntegrationSuiteRun label then
                        do! timedAsyncSuite label f
                    else
                        do! timedAsync label f

            return summary ()
    }
