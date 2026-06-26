module VibeFs.Tests.Tests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.ReviewTests
open VibeFs.Tests.AgentTests
open VibeFs.Tests.AgentNudgeSpecs
open VibeFs.Tests.KernelTests
open VibeFs.Tests.KernelPromptSpecs
open VibeFs.Tests.FuzzyTests
open VibeFs.Tests.ShellTests
open VibeFs.Tests.DynTests
open VibeFs.Tests.DelegateTests
open VibeFs.Tests.DelegateToolsCodecTests
open VibeFs.Tests.ResolveAiSettingsTests
open VibeFs.Tests.IntegrationPluginTests
open VibeFs.Tests.IntegrationEventTests
open VibeFs.Tests.IntegrationDedupTests
open VibeFs.Tests.IntegrationToolSpecCatalog
open VibeFs.Tests.IntegrationOpencodeReviewSpecs
open VibeFs.Tests.IntegrationChatTests
open VibeFs.Tests.WorkBacklogTests
open VibeFs.Tests.MethodologyTests
open VibeFs.Tests.KnowledgeGraphTests
open VibeFs.Tests.KnowledgeGraphFileTests
open VibeFs.Tests.KnowledgeGraphKernelTests
open VibeFs.Tests.TitleFetchGuardTests
open VibeFs.Tests.TestsTestBody
open VibeFs.Tests.TestsArchitectureRegistry
open VibeFs.Tests.TestsEntriesCore
open VibeFs.Tests.TestsEntriesCodec
open VibeFs.Tests.TestsEntriesOmp
open VibeFs.Tests.ReviewReplaySyncTests
open VibeFs.Tests.CapsSynthCommonTests
open VibeFs.Tests.CapsFileCacheTests
open VibeFs.Tests.SubagentPromptBuildTests
open VibeFs.Tests.SubagentSpawnTests
open VibeFs.Tests.WebToolsCodecTests
open VibeFs.Tests.ReviewToolsCodecTests
open VibeFs.Tests.KnowledgeGraphToolsCodecTests
open VibeFs.Tests.ExecutorToolsCodecTests
open VibeFs.Tests.ToolArgsDecodeTests
open VibeFs.Tests.ToolResultWireTests
open VibeFs.Tests.SubagentToolExecuteTests
open VibeFs.Tests.FileToolsCodecTests
open VibeFs.Tests.FuzzyToolsCodecTests
open VibeFs.Tests.WorkBacklogToolsCodecTests
open VibeFs.Tests.PatchToolsCodecTests
open VibeFs.Tests.HostMessagePartCodecTests
open VibeFs.Tests.MessagingPartCodecTests
open VibeFs.Tests.ToolContextCodecTests
open VibeFs.Tests.OpencodeContextCodecTests
open VibeFs.Tests.OpencodeSessionPromptCodecTests
open VibeFs.Tests.OpencodeSessionSpawnCodecTests
open VibeFs.Tests.SessionIoPromptBodyTests
open VibeFs.Tests.OpencodeAgentConfigCodecTests
open VibeFs.Tests.OpencodeSessionEventCodecTests
open VibeFs.Tests.MuxAiSettingsCodecTests
open VibeFs.Tests.MuxAiSettingsIntegrationTests
open VibeFs.Tests.AgentConfigApplyTests
open VibeFs.Tests.KnowledgeGraphWorkflowTests
open VibeFs.Tests.KnowledgeGraphBookkeeperLaunchTests
open VibeFs.Tests.KnowledgeGraphMaintenanceRunTests
open VibeFs.Tests.SessionExecutorScopeTests
open VibeFs.Tests.OmpKernelTests
open VibeFs.Tests.OmpSessionToolsTests
open VibeFs.Tests.OmpWebFetchTests
open VibeFs.Tests.OmpCapsTests
open VibeFs.Tests.OmpFuzzyTests
open VibeFs.Tests.OmpPluginTests
open VibeFs.Tests.OmpPluginTestsAgentEnd
open VibeFs.Tests.OmpReviewTests
open VibeFs.Tests.OmpHelpersTests
open VibeFs.Tests.OmpRunnerTests
open VibeFs.Tests.OmpContextTransformTests
open VibeFs.Tests.OmpChildSessionTests
open VibeFs.Tests.OmpAgentConfigTests
open VibeFs.Tests.OmpHookExecuteTests
open VibeFs.Tests.OmpKnowledgeGraphRuntimeTests
open VibeFs.Tests.OmpSessionLifecycleTests
open VibeFs.Tests.OmpPluginCoreTests
open VibeFs.Tests.OmpTitleFetchGuardTests
open VibeFs.Tests.OmpMagicTodoTests
open VibeFs.Tests.OmpPluginCoreIntegrationTests
open VibeFs.Tests.SubagentIoTests
open VibeFs.Omp.Plugin

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
            let arr : string[] = unbox argvObj
            arr |> Array.exists (fun a -> a = "--verbose")
        with _ -> false
    envHit || cliHit

let private initVerboseLog () : unit =
    if verboseEnabledFromArgs () then
        let ts = System.DateTime.Now.ToString("yyyyMMdd-HHmmss")
        let logDir = "tests/logs"
        let logPath = sprintf "%s/%s.verbose.log" logDir ts
        try mkdirSync logDir (createObj [ "recursive", box true ]) with _ -> ()
        let header =
            sprintf "# vibe-fs verbose test log\n# timestamp: %s\n# switch: %s\n"
                ts (if (envVerbose ()) = "1" then "VIBE_FS_TEST_VERBOSE=1" else "--verbose")
        appendFile logPath header "utf8"
        Assert.setVerbose (Some logPath)

let private integrationToolFlatTests : (string * TestBody) list =
    integrationToolSpecs ()
    |> List.map (fun (shortName, spec) -> "IntegrationTool." + shortName, Async spec)

let private tests : (string * TestBody) list =
    coreTestEntries ()
    @ (architectureTestEntries ())
    @ codecTestEntries ()
    @ ompTestEntries ()
    @ integrationToolFlatTests

let private matchesSelector (selectors: string array) (label: string) =
    selectors.Length = 0
    || selectors
       |> Array.exists (fun selector ->
           let trimmed = selector.Trim ()
           trimmed.Length > 0 && label.StartsWith trimmed)

let private selectedTests (selectors: string array) =
    tests |> List.filter (fun (label, _) -> matchesSelector selectors label)

let runAll (args: string array) : JS.Promise<int> =
    promise {
        clearFailuresForRun ()
        let selectors =
            args |> Array.filter (fun a -> a <> "--verbose" && a <> "-v")
        initVerboseLog ()
        resetOmpPluginTestState ()
        let runnableTests = selectedTests selectors
        if List.isEmpty runnableTests then
            printfn "No tests matched selectors: %A" args
            return 1
        else
            let isIntegrationSuiteRun (label: string) =
                label.StartsWith "Integration" && label.EndsWith ".run"

            for (label, body) in runnableTests do
                match body with
                | Sync f -> timed label f
                | Async f ->
                    if isIntegrationSuiteRun label then do! timedAsyncSuite label f
                    else do! timedAsync label f
            return summary ()
    }
