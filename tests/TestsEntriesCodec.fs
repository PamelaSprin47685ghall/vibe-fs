module Wanxiangshu.Tests.TestsEntriesCodec

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
open Wanxiangshu.Tests.ReviewReplaySyncTests
open Wanxiangshu.Tests.ToolArgsCoerceTests
open Wanxiangshu.Tests.CapsSynthCommonTests
open Wanxiangshu.Tests.CapsFileCacheTests
open Wanxiangshu.Tests.CapsFormatTests
open Wanxiangshu.Tests.SubagentPromptBuildTests
open Wanxiangshu.Tests.SubagentSpawnTests
open Wanxiangshu.Tests.WebToolsCodecTests
open Wanxiangshu.Tests.ReviewToolsCodecTests
open Wanxiangshu.Tests.ExecutorToolsCodecTests
open Wanxiangshu.Tests.ToolArgsDecodeTests
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
open Wanxiangshu.Tests.OpencodeClientCodecTests
open Wanxiangshu.Tests.OpencodeSessionEventCodecTests
open Wanxiangshu.Tests.OpencodeSessionEventCodecCommonTests
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
open Wanxiangshu.Tests.SubagentIoTests
open Wanxiangshu.Tests.SubagentCleanupCompletionTests
open Wanxiangshu.Tests.TestsTestBody

let codecTestEntries () : (string * TestBody) list =
    [ "SubagentToolPolicyTests.run", Sync(sync SubagentToolPolicyTests.run)
      "SubagentPromptBuildTests.run", Sync(sync SubagentPromptBuildTests.run)
      "SubagentSpawnTests.run", Async SubagentSpawnTests.run
      "ToolArgsDecodeTests.run", Sync(sync ToolArgsDecodeTests.run)
      "ToolArgsCoerceTests.run", Sync(sync ToolArgsCoerceTests.run)
      "ToolResultWireTests.run", Sync(sync ToolResultWireTests.run)
      "SubagentToolExecuteTests.run", Async SubagentToolExecuteTests.run
      "SubagentCleanupCompletionTests.run", Async SubagentCleanupCompletionTests.run
      "WebToolsCodecTests.run", Sync(sync WebToolsCodecTests.run)
      "ReviewToolsCodecTests.run", Sync(sync ReviewToolsCodecTests.run)
      "FileToolsCodecTests.run", Sync(sync FileToolsCodecTests.run)
      "FuzzyToolsCodecTests.run", Sync(sync FuzzyToolsCodecTests.run)
      "WorkBacklogToolsCodecTests.run", Sync(sync WorkBacklogToolsCodecTests.run)
      "PatchToolsCodecTests.run", Sync(sync PatchToolsCodecTests.run)
      "HostMessageCodecTests.run", Sync(sync HostMessageCodecTests.run)
      "MessagingCodecTests.run", Sync(sync MessagingCodecTests.run)
      "ExecutorToolsCodecTests.run", Sync(sync ExecutorToolsCodecTests.run)
      "ToolContextCodecTests.run", Sync(sync ToolContextCodecTests.run)
      "OpencodeContextCodecTests.run", Sync(sync OpencodeContextCodecTests.run)
      "OpencodeSessionPromptCodecTests.run", Sync(sync OpencodeSessionPromptCodecTests.run)
      "OpencodeSessionSpawnCodecTests.run", Sync(sync OpencodeSessionSpawnCodecTests.run)
      "SessionIoPromptBodyTests.run", Sync(sync SessionIoPromptBodyTests.run)
      "OpencodeAgentConfigCodecTests.run", Sync(sync OpencodeAgentConfigCodecTests.run)
      "OpencodeClientCodecTests.run", Sync(sync OpencodeClientCodecTests.run)
      "OpencodeSessionEventCodecTests.run", Sync(sync OpencodeSessionEventCodecTests.run)
      "OpencodeSessionEventCodecCommonTests.run", Sync(sync OpencodeSessionEventCodecCommonTests.run)
      "MuxAiSettingsCodecTests.run", Sync(sync MuxAiSettingsCodecTests.run)
      "MuxAiSettingsIntegrationTests.run", Async MuxAiSettingsIntegrationTests.run
      "AgentConfigApplyTests.run", Sync(sync AgentConfigApplyTests.run)
      "SessionExecutorScopeTests.run", Async SessionExecutorScopeTests.run
      "CapsSynthCommonTests.run", Sync(sync CapsSynthCommonTests.run)
      "CapsFileCacheTests.run", Async CapsFileCacheTests.run
      "CapsFormatTests.run", Sync(sync CapsFormatTests.run)
      "ReviewReplaySyncTests.run", Sync(sync ReviewReplaySyncTests.run) ]
