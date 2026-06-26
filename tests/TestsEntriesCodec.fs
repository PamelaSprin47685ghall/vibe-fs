module VibeFs.Tests.TestsEntriesCodec

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
open VibeFs.Tests.TestsTestBody

let codecTestEntries () : (string * TestBody) list =
    [
    "SubagentToolPolicyTests.run", Sync (sync SubagentToolPolicyTests.run)
    "SubagentPromptBuildTests.run", Sync (sync SubagentPromptBuildTests.run)
    "SubagentSpawnTests.run", Async SubagentSpawnTests.run
    "ToolArgsDecodeTests.run", Sync (sync ToolArgsDecodeTests.run)
    "ToolResultWireTests.run", Sync (sync ToolResultWireTests.run)
    "SubagentToolExecuteTests.run", Async SubagentToolExecuteTests.run
    "WebToolsCodecTests.run", Sync (sync WebToolsCodecTests.run)
    "ReviewToolsCodecTests.run", Sync (sync ReviewToolsCodecTests.run)
    "KnowledgeGraphToolsCodecTests.run", Sync (sync KnowledgeGraphToolsCodecTests.run)
    "FileToolsCodecTests.run", Sync (sync FileToolsCodecTests.run)
    "FuzzyToolsCodecTests.run", Sync (sync FuzzyToolsCodecTests.run)
    "WorkBacklogToolsCodecTests.run", Sync (sync WorkBacklogToolsCodecTests.run)
    "PatchToolsCodecTests.run", Sync (sync PatchToolsCodecTests.run)
    "HostMessagePartCodecTests.run", Sync (sync HostMessagePartCodecTests.run)
    "MessagingPartCodecTests.run", Sync (sync MessagingPartCodecTests.run)
    "ExecutorToolsCodecTests.run", Sync (sync ExecutorToolsCodecTests.run)
    "ToolContextCodecTests.run", Sync (sync ToolContextCodecTests.run)
    "OpencodeContextCodecTests.run", Sync (sync OpencodeContextCodecTests.run)
    "OpencodeSessionPromptCodecTests.run", Sync (sync OpencodeSessionPromptCodecTests.run)
    "OpencodeSessionSpawnCodecTests.run", Sync (sync OpencodeSessionSpawnCodecTests.run)
    "SessionIoPromptBodyTests.run", Sync (sync SessionIoPromptBodyTests.run)
    "OpencodeAgentConfigCodecTests.run", Sync (sync OpencodeAgentConfigCodecTests.run)
    "OpencodeSessionEventCodecTests.run", Sync (sync OpencodeSessionEventCodecTests.run)
    "MuxAiSettingsCodecTests.run", Sync (sync MuxAiSettingsCodecTests.run)
    "MuxAiSettingsIntegrationTests.run", Async MuxAiSettingsIntegrationTests.run
    "AgentConfigApplyTests.run", Sync (sync AgentConfigApplyTests.run)
    "KnowledgeGraphWorkflowTests.run", Async KnowledgeGraphWorkflowTests.run
    "KnowledgeGraphBookkeeperLaunchTests.run", Async KnowledgeGraphBookkeeperLaunchTests.run
    "KnowledgeGraphMaintenanceRunTests.run", Async KnowledgeGraphMaintenanceRunTests.run
    "SessionExecutorScopeTests.run", Async SessionExecutorScopeTests.run
    "CapsSynthCommonTests.run", Sync (sync CapsSynthCommonTests.run)
    "CapsFileCacheTests.run", Async CapsFileCacheTests.run
    "ReviewReplaySyncTests.run", Sync (sync ReviewReplaySyncTests.run)
    ]
