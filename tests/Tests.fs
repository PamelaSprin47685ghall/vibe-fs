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
open Wanxiangshu.Tests.IntegrationOpencodeContractTests
open Wanxiangshu.Tests.IntegrationChatTests
open Wanxiangshu.Tests.WorkBacklogTests
open Wanxiangshu.Tests.MethodologyTests
open Wanxiangshu.Tests.TestRunnerBehaviorTests

open Wanxiangshu.Tests.TitleFetchGuardTests
open Wanxiangshu.Tests.TestsTestBody
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
open Wanxiangshu.Tests.ContextBudgetSpecs
open Wanxiangshu.Tests.ContextBudgetHookTests
open Wanxiangshu.Tests.ContextBudgetNoReinjectTests
open Wanxiangshu.Tests.ContextBudgetAfterTodoTests
open Wanxiangshu.Tests.ContextBudgetIntegrationTests
open Wanxiangshu.Tests.ContextBudgetRealApiSpecs
open Wanxiangshu.Tests.ContextBudgetEstimateTests

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
open Wanxiangshu.Tests.OmpHelpersTests
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
open Wanxiangshu.Tests.KernelHelpersTests

open Wanxiangshu.Tests.ReviewPromptsFormatTests
open Wanxiangshu.Tests.Phase0BaselineTests
open Wanxiangshu.Tests.CommandProcessorE2ETests
open Wanxiangshu.Tests.ReplayEquivalenceTests
open Wanxiangshu.Tests.FlowKernelTests
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

let private verboseEnabledFromArgs () : bool = true

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
            sprintf "# vibe-fs verbose test log\n# timestamp: %s\n# switch: ALWAYS_ON\n" ts

        appendFile logPath header "utf8"
        Assert.setVerbose (Some logPath)

let private integrationToolFlatTests: (string * TestBody) list =
    integrationToolSpecs ()
    |> List.map (fun (shortName, spec) -> "IntegrationTool." + shortName, Async spec)

let private allOtherTests: (string * TestBody) list =
    coreTestEntries ()
    @ wanxiangzhenTestEntries ()
    @ codecTestEntries ()
    @ ompTestEntries ()
    @ [ "Phase0BaselineTests.run", TestBody.Sync Phase0BaselineTests.run
        "CommandProcessorE2ETests.run", TestBody.Async CommandProcessorE2ETests.run
        "ReplayEquivalenceTests.run", TestBody.Sync ReplayEquivalenceTests.run
        "FlowKernelTests.run", TestBody.Async FlowKernelTests.run
        "ReactiveTests.run", TestBody.Sync ReactiveTests.run
        "ResourcePlanTests.run", TestBody.Sync ResourcePlanTests.run
        "SessionOverviewTests.run", TestBody.Sync SessionOverviewTests.run
        "ContextBudgetSpecs.run", TestBody.Sync(sync ContextBudgetSpecs.run)
        "ContextBudgetHookTests.run", TestBody.Async ContextBudgetHookTests.run
        "ContextBudgetNoReinjectTests.run", TestBody.Async ContextBudgetNoReinjectTests.run
        "ContextBudgetAfterTodoTests.run", TestBody.Async ContextBudgetAfterTodoTests.run
        "ContextBudgetIntegrationTests.run", TestBody.Async ContextBudgetIntegrationTests.run
        "ContextBudgetRealApiSpecs.run", TestBody.Async ContextBudgetRealApiSpecs.run
        "ContextBudgetEstimateTests.run", TestBody.Async ContextBudgetEstimateTests.run ]
    @ [ "IntegrationOpenCodeContractTests.run", TestBody.Async(fun () -> IntegrationOpencodeContractTests.runAll [||]) ]
    @ integrationToolFlatTests

let private tests: (string * TestBody) list =
    allOtherTests
    @ [ "TestRunnerBehaviorTests.defaultSuiteHasNoArchitectureLabels",
        TestBody.Sync(fun () -> defaultSuiteHasNoArchitectureLabels (allOtherTests |> List.map fst)) ]

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
        try
            let p: obj = Fable.Core.JsInterop.emitJsExpr () "process"
            p?env?("WANXIANGSHU_TEST") <- "true"
        with _ ->
            ()

        clearFailuresForRun ()
        Assert.setSilent false
        let selectors = args
        initVerboseLog ()
        PluginComposition.reviewStore.clearReviewSessions ()
        RunnerBackground.clearRunnerLogsForTest ExecutorTools.ompScope

        let runnableTests = selectedTests selectors

        if List.isEmpty runnableTests then
            printfn "No tests matched selectors: %A" args
            return 1
        else
            let isIntegrationSuiteRun (label: string) =
                (label.StartsWith "Integration" && label.EndsWith ".run")
                || (label = "OmpExecutorToolsTests.run")

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
