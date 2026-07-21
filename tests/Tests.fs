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
open Wanxiangshu.Tests.ExecutorSpawnPathTests
open Wanxiangshu.Tests.DynTests
open Wanxiangshu.Tests.DelegateTests
open Wanxiangshu.Tests.DelegateToolsCodecTests
open Wanxiangshu.Tests.ResolveAiSettingsTests
open Wanxiangshu.Tests.IntegrationPluginTests
open Wanxiangshu.Tests.IntegrationEventTests
open Wanxiangshu.Tests.IntegrationToolSpecCatalog
open Wanxiangshu.Tests.IntegrationOpencodeReviewSpecs
open Wanxiangshu.Tests.IntegrationOpencodeContractTests
open Wanxiangshu.Tests.ReplayEquivalenceTests
open Wanxiangshu.Tests.IntegrationChatTests
open Wanxiangshu.Tests.WorkBacklogTests
open Wanxiangshu.Tests.MethodologyTests
open Wanxiangshu.Tests.TestRunnerBehaviorTests
open Wanxiangshu.E2e

open Wanxiangshu.Tests.TitleFetchGuardTests
open Wanxiangshu.Tests.TestsTestBody
open Wanxiangshu.Tests.TestsEntriesCore
open Wanxiangshu.Tests.TestsEntriesWanxiangzhen
open Wanxiangshu.Tests.TestsEntriesCodec
open Wanxiangshu.Tests.TestsEntriesOmp
open Wanxiangshu.Tests.CapsSynthCommonTests
open Wanxiangshu.Tests.CapsFileCacheTests
open Wanxiangshu.Tests.SubagentPromptBuildTests
open Wanxiangshu.Tests.SubagentSpawnTests
open Wanxiangshu.Tests.WebToolsCodecTests
open Wanxiangshu.Tests.ReviewToolsCodecTests
open Wanxiangshu.Tests.ContextBudgetSpecs
open Wanxiangshu.Tests.ContextBudgetHookTests
open Wanxiangshu.Tests.ContextBudgetNoReinjectTests
open Wanxiangshu.Tests.ContextBudgetAfterTodoTests
open Wanxiangshu.Tests.ContextBudgetIntegrationTests
open Wanxiangshu.Tests.ContextBudgetRealApiSpecs
open Wanxiangshu.Tests.ContextBudgetEstimateTests
open Wanxiangshu.Tests.ContextBudgetPipelineNudgeTests
open Wanxiangshu.Tests.ContextBudgetCalibrationTests

open Wanxiangshu.Tests.ExecutorToolsCodecTests
open Wanxiangshu.Tests.ExecutorTests
open Wanxiangshu.Tests.ToolArgsDecodeTests
open Wanxiangshu.Tests.ToolArgsCoerceTests
open Wanxiangshu.Tests.ToolResultWireTests
open Wanxiangshu.Tests.SubagentToolExecuteTests
open Wanxiangshu.Tests.FileToolsCodecTests
open Wanxiangshu.Tests.FuzzyToolsCodecTests
open Wanxiangshu.Tests.WorkBacklogToolsCodecTests
open Wanxiangshu.Tests.PatchToolsCodecTests
open Wanxiangshu.Tests.HostMessageCodecTests
open Wanxiangshu.Tests.MessagingCodecTests
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
open Wanxiangshu.Tests.OmpToolingTests
open Wanxiangshu.Tests.OmpRunnerTests
open Wanxiangshu.Tests.OmpContextTransformTests
open Wanxiangshu.Tests.OmpAgentConfigTests
open Wanxiangshu.Tests.OmpHookExecuteTests

open Wanxiangshu.Tests.OmpSessionLifecycleTests
open Wanxiangshu.Tests.OmpPluginCoreTests
open Wanxiangshu.Tests.OmpTitleFetchGuardTests
open Wanxiangshu.Tests.OmpMagicTodoTests
open Wanxiangshu.Tests.OmpPluginCoreIntegrationTests
open Wanxiangshu.Tests.EventDrivenHarnessDemo
open Wanxiangshu.Tests.SubagentIoTests
open Wanxiangshu.Tests.ToolCatalogClassificationTests
open Wanxiangshu.Tests.ToolOutputInfoTests
open Wanxiangshu.Tests.KernelPolicyTests

open Wanxiangshu.Tests.ReviewPromptsFormatTests
open Wanxiangshu.Tests.SubsessionGoldenTrajectoryTests
open Wanxiangshu.Tests.ReactiveTests
open Wanxiangshu.Tests.ResourcePlanTests
open Wanxiangshu.Tests.SessionOverviewTests
open Wanxiangshu.Hosts.Omp
open Wanxiangshu.Hosts.Omp.Plugin
open Wanxiangshu.Hosts.Omp.PluginComposition
open Wanxiangshu.Runtime

[<Import("appendFileSync", "node:fs")>]
let private appendFile (path: string) (content: string) (encoding: string) : unit = jsNative

[<Import("mkdirSync", "node:fs")>]
let private mkdirSync (path: string) (opts: obj) : unit = jsNative

let private verboseEnabledFromArgs (args: string array) : bool =
    try
        let p: obj = Fable.Core.JsInterop.emitJsExpr () "process"
        let envVar = string p?env?("VIBE_FS_TEST_VERBOSE")
        envVar = "1" || envVar = "true" || Array.contains "--verbose" args
    with _ ->
        Array.contains "--verbose" args

let private initVerboseLog (args: string array) : unit =
    if verboseEnabledFromArgs args then
        let ts = System.DateTime.Now.ToString("yyyyMMdd-HHmmss")
        let logDir = "tests/logs"
        let logPath = sprintf "%s/%s.verbose.log" logDir ts

        try
            mkdirSync logDir (createObj [ "recursive", box true ])
        with _ ->
            ()

        let header =
            sprintf "# vibe-fs verbose test log\n# timestamp: %s\n# switch: ALWAYS_ON\n" ts

        appendFile logPath header "utf8"
        Assert.setVerbose (Some logPath)

let private integrationToolFlatTests: (string * TestBody) list =
    integrationToolSpecs ()
    |> List.map (fun (shortName, spec) -> "IntegrationTool." + shortName, Async spec)

/// Integration harness suites that start real OpenCode plugin processes. They
/// already enforce internal per-operation deadlines and cannot complete inside
/// the generic 1s suite cap, so the runner grants them a dedicated ceiling.
let private integrationHarnessSuiteLabels =
    [ "Integration.OpencodePluginTests.run"
      "Integration.MimocodePluginTests.run"
      "Integration.MimoTuiPluginTests.run"
      "IntegrationOpenCodeContractTests.run" ]

let private allOtherTests: (string * TestBody) list =
    coreTestEntries ()
    @ wanxiangzhenTestEntries ()
    @ codecTestEntries ()
    @ ompTestEntries ()
    @ [ "SubsessionGoldenTrajectoryTests.run", TestBody.Sync SubsessionGoldenTrajectoryTests.run
        "ReactiveTests.run", TestBody.Sync ReactiveTests.run
        "ResourcePlanTests.run", TestBody.Sync ResourcePlanTests.run
        "SessionOverviewTests.run", TestBody.Sync SessionOverviewTests.run
        "ReplayEquivalenceTests.run", TestBody.Sync ReplayEquivalenceTests.run
        "ContextBudgetSpecs.run", TestBody.Sync(sync ContextBudgetSpecs.run)
        "ContextBudgetHookTests.run", TestBody.Async ContextBudgetHookTests.run
        "ContextBudgetNoReinjectTests.run", TestBody.Async ContextBudgetNoReinjectTests.run
        "ContextBudgetAfterTodoTests.run", TestBody.Async ContextBudgetAfterTodoTests.run
        "ContextBudgetIntegrationTests.run", TestBody.Async ContextBudgetIntegrationTests.run
        "ContextBudgetRealApiSpecs.run", TestBody.Async ContextBudgetRealApiSpecs.run
        "ContextBudgetEstimateTests.run", TestBody.Async ContextBudgetEstimateTests.run
        "ContextBudgetPipelineNudgeTests.spec_applyContextBudget_mustSeeFinalOutboundAfterAllStages",
        TestBody.Async ContextBudgetPipelineNudgeTests.spec_applyContextBudget_mustSeeFinalOutboundAfterAllStages
        "ContextBudgetCalibrationTests.run", TestBody.Sync(sync ContextBudgetCalibrationTests.run) ]

let private integrationTests: (string * TestBody) list =
    [ "Integration.OpencodePluginTests.run",
      TestBody.Async(fun () -> OpencodePluginTests.runAll [||] |> Promise.map ignore)
      "Integration.MimocodePluginTests.run",
      TestBody.Async(fun () -> MimocodePluginTests.runAll [||] |> Promise.map ignore)
      "Integration.MimoTuiPluginTests.run",
      TestBody.Async(fun () -> MimoTuiPluginTests.runAll [||] |> Promise.map ignore)
      "IntegrationOpenCodeContractTests.run", TestBody.Async(fun () -> IntegrationOpencodeContractTests.runAll [||]) ]
    @ integrationToolFlatTests

let private qualityGatesTests: (string * TestBody) list =
    [ "ArchitectureGatesTests.run", TestBody.Sync(sync Wanxiangshu.Tests.ArchitectureGatesTests.run) ]

let private tests: (string * TestBody) list =
    allOtherTests
    @ integrationTests
    @ qualityGatesTests
    @ [ "TestRunnerBehaviorTests.defaultSuiteHasNoArchitectureLabels",
        TestBody.Sync(fun () -> defaultSuiteHasNoArchitectureLabels (allOtherTests |> List.map fst)) ]

let private matchesSelector (selectors: string array) (label: string) =
    selectors.Length = 0
    || selectors
       |> Array.exists (fun selector ->
           let trimmed = selector.Trim()
           trimmed.Length > 0 && label.ToLower().Contains(trimmed.ToLower()))

let private selectedTests (selectors: string array) =
    let allTestList =
        if selectors.Length > 0 && selectors.[0] = "L0" then
            allOtherTests
        elif selectors.Length > 0 && selectors.[0] = "L2" then
            integrationTests
        elif selectors.Length > 0 && selectors.[0] = "L4" then
            qualityGatesTests
        else
            tests

    let filterSelectors =
        if
            selectors.Length > 0
            && (selectors.[0] = "L0" || selectors.[0] = "L2" || selectors.[0] = "L4")
        then
            selectors |> Array.skip 1
        else
            selectors

    allTestList
    |> List.filter (fun (label, _) -> matchesSelector filterSelectors label)

let runAll (args: string array) : JS.Promise<int> =
    promise {
        try
            let p: obj = Fable.Core.JsInterop.emitJsExpr () "process"
            p?env?("WANXIANGSHU_TEST") <- "true"
        with _ ->
            ()

        clearFailuresForRun ()
        Assert.disableGlobalClear ()
        let silent = Array.contains "--silent" args || Array.contains "--quiet" args
        Assert.setSilent silent

        let cleanSelectors =
            args
            |> Array.filter (fun arg -> arg <> "--silent" && arg <> "--quiet" && arg <> "--verbose")

        initVerboseLog args
        PluginComposition.reviewStore.clearReviewSessions ()
        RunnerBackground.clearRunnerLogsForTest ExecutorTools.ompScope

        let runnableTests = selectedTests cleanSelectors

        if List.isEmpty runnableTests then
            if not silent then
                printfn "No tests matched selectors: %A" cleanSelectors

            return 1
        else
            let isIntegrationSuiteRun (label: string) =
                (label.StartsWith "Integration" && label.EndsWith ".run")
                || (label = "OmpExecutorToolsTests.run")
                || (label = "ReaderWriterLockTests.run")

            for (label, body) in runnableTests do
                if not silent then
                    printfn "[RUN] %s" label

                match body with
                | Sync f -> timed label f
                | Async f ->
                    if List.contains label integrationHarnessSuiteLabels then
                        do! timedAsyncSuiteWithTimeout label Assert.integrationHarnessSuiteTimeoutMs f
                    elif isIntegrationSuiteRun label then
                        do! timedAsyncSuite label f
                    else
                        do! timedAsync label f

            return summary ()
    }
