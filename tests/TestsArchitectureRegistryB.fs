module Wanxiangshu.Tests.TestsArchitectureRegistryB

open Fable.Core
open Wanxiangshu.Tests.ArchitectureTestsFoundationB
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsFoundation
open Wanxiangshu.Tests.ArchitectureTestsMessageTransform
open Wanxiangshu.Tests.ArchitectureTestsSubagent
open Wanxiangshu.Tests.ArchitectureTestsSubagentCatalog
open Wanxiangshu.Tests.ArchitectureTestsSubagentSession
open Wanxiangshu.Tests.ArchitectureTestsSubagentToolExec
open Wanxiangshu.Tests.ArchitectureTestsRuntime
open Wanxiangshu.Tests.ArchitectureTestsRuntimeSession
open Wanxiangshu.Tests.ArchitectureTestsWireToolExec
open Wanxiangshu.Tests.ArchitectureTestsWireHook
open Wanxiangshu.Tests.ArchitectureTestsWirePipeline
open Wanxiangshu.Tests.ArchitectureTestsWirePayload
open Wanxiangshu.Tests.ArchitectureTestsMuxToolCore
open Wanxiangshu.Tests.ArchitectureTestsMuxToolAux
open Wanxiangshu.Tests.ArchitectureTestsOpencodeTools
open Wanxiangshu.Tests.ArchitectureTestsOpencodeToolsReview
open Wanxiangshu.Tests.ArchitectureTestsOpencodeToolsSearch
open Wanxiangshu.Tests.ArchitectureTestsOpencodeToolsExecutor
open Wanxiangshu.Tests.TestsTestBody
open Wanxiangshu.Tests.E2eHarnessContractTests
open Wanxiangshu.Tests.ArchitectureTestsTimeIndependence

let architectureTestEntriesPartB () : (string * TestBody) list =
    [ "ArchitectureTests.messageTransformUsesBacklogSessionOpsFrom",
      Sync(sync ArchitectureTestsWirePipeline.messageTransformUsesBacklogSessionOpsFrom)
      "ArchitectureTests.muxReviewUsesToolCopy", Sync(sync ArchitectureTestsMuxToolAux.muxReviewUsesToolCopy)
      "ArchitectureTests.muxReviewUsesFromMuxConfig", Sync(sync ArchitectureTestsMuxToolAux.muxReviewUsesFromMuxConfig)
      "ArchitectureTests.muxReviewUsesReviewToolsCodec",
      Sync(sync ArchitectureTestsMuxToolAux.muxReviewUsesReviewToolsCodec)
      "ArchitectureTests.muxPluginCatalogToolExecuteAfterUsesLivelockGuard",
      Sync(sync ArchitectureTestsMuxToolAux.muxPluginCatalogToolExecuteAfterUsesLivelockGuard)
      "ArchitectureTests.muxSlashCommandsLoopUsesDepsDirectory",
      Sync(sync ArchitectureTestsMuxToolAux.muxSlashCommandsLoopUsesDepsDirectory)
      "ArchitectureTests.executeMuxSubagentToolUsesSpawnRoleOnly",
      Sync(sync ArchitectureTestsSubagentToolExec.executeMuxSubagentToolUsesSpawnRoleOnly)
      "ArchitectureTests.subagentToolExecuteEmptyBatchGuard",
      Sync(sync ArchitectureTestsSubagentToolExec.subagentToolExecuteEmptyBatchGuard)
      "ArchitectureTests.opencodeReviewUsesToolCopy",
      Sync(sync ArchitectureTestsOpencodeToolsReview.opencodeReviewUsesToolCopy)
      "ArchitectureTests.opencodeReviewUsesFromOpencode",
      Sync(sync ArchitectureTestsOpencodeToolsReview.opencodeReviewUsesFromOpencode)
      "ArchitectureTests.opencodeReviewUsesReviewToolsCodec",
      Sync(sync ArchitectureTestsOpencodeToolsReview.opencodeReviewUsesReviewToolsCodec)
      "ArchitectureTests.opencodeNudgeDoesNotReadReviewStoreForLoopState",
      Sync(sync ArchitectureTestsOpencodeToolsReview.opencodeNudgeDoesNotReadReviewStoreForLoopState)
      "ArchitectureTests.opencodeSubagentToolsUsesToolArgsDecode",
      Sync(sync ArchitectureTestsSubagentToolExec.opencodeSubagentToolsUsesToolArgsDecode)
      "ArchitectureTests.muxSubagentToolsUsesSimpleArgsCodec",
      Sync(sync ArchitectureTestsMuxToolAux.muxSubagentToolsUsesSimpleArgsCodec)
      "ArchitectureTests.subagentToolsUseDecodeIntentsField",
      Sync(sync ArchitectureTestsSubagent.subagentToolsUseDecodeIntentsField)
      "ArchitectureTests.subagentToolsUseToolCatalogRequiredKeys",
      Sync(sync ArchitectureTestsSubagentCatalog.subagentToolsUseToolCatalogRequiredKeys)
      "ArchitectureTests.kernelToolArgsExists", Sync(sync ArchitectureTestsSubagentCatalog.kernelToolArgsExists)
      "ArchitectureTests.toolArgsDecodeExists", Sync(sync ArchitectureTestsSubagentCatalog.toolArgsDecodeExists)
      "ArchitectureTests.toolArgsDecodeCoversMajorTools",
      Sync(sync ArchitectureTestsSubagentCatalog.toolArgsDecodeCoversMajorTools)
      "ArchitectureTests.decodedToolInvocationNoObj",
      Sync(sync ArchitectureTestsSubagentCatalog.decodedToolInvocationNoObj)
      "ArchitectureTests.muxSubagentToolsUsesToolArgsDecode",
      Sync(sync ArchitectureTestsSubagentToolExec.muxSubagentToolsUsesToolArgsDecode)
      "ToolArgsDecodeTests.run", Sync(sync ToolArgsDecodeTests.run)
      "ToolResultWireTests.run", Sync(sync ToolResultWireTests.run)
      "SubagentToolExecuteTests.run", Async SubagentToolExecuteTests.run
      "ArchitectureTests.opencodeSubagentToolsUsesToolArgsDecode",
      Sync(sync ArchitectureTestsSubagentToolExec.opencodeSubagentToolsUsesToolArgsDecode)
      "ArchitectureTests.opencodeSubagentToolsUsesSimpleArgsCodec",
      Sync(sync ArchitectureTestsSubagentSession.opencodeSubagentToolsUsesSimpleArgsCodec)
      "ArchitectureTests.sessionIoRunSubagentReturnsResult",
      Sync(sync ArchitectureTestsSubagentSession.sessionIoRunSubagentReturnsResult)
      "ArchitectureTests.commandHooksUsesToolCopyReviewMessages",
      Sync(sync ArchitectureTestsSubagentSession.commandHooksUsesToolCopyReviewMessages)
      "ArchitectureTests.commandHooksUsesRegisterLoopReviewCommands",
      Sync(sync ArchitectureTestsWireHook.commandHooksUsesRegisterLoopReviewCommands)
      "ArchitectureTests.opencodeHookExecuteUsesHookArgsHelpers",
      Sync(sync ArchitectureTestsWireToolExec.opencodeHookExecuteUsesHookArgsHelpers)
      "ArchitectureTests.opencodeCommandHooksUsesPartsWriter",
      Sync(sync ArchitectureTestsWireToolExec.opencodeCommandHooksUsesPartsWriter)
      "ArchitectureTests.muxHookOutputUsesMuxHookInputCodec",
      Sync(sync ArchitectureTestsWireToolExec.muxHookOutputUsesMuxHookInputCodec)
      "ArchitectureTests.subagentToolsUseSubagentSpawn",
      Sync(sync ArchitectureTestsSubagentToolExec.subagentToolsUseSubagentSpawn)
      "ArchitectureTests.muxWrappersSyntaxUsesFromMuxConfig",
      Sync(sync ArchitectureTestsMuxToolAux.muxWrappersSyntaxUsesFromMuxConfig)
      "ArchitectureTests.muxHostToolsFuzzyUsesToolCopy",
      Sync(sync ArchitectureTestsMuxToolCore.muxHostToolsFuzzyUsesToolCopy)
      "ArchitectureTests.muxHostToolsFuzzyUsesFromMuxConfig",
      Sync(sync ArchitectureTestsMuxToolCore.muxHostToolsFuzzyUsesFromMuxConfig)
      "ArchitectureTests.muxHostToolsFuzzyUsesFuzzyToolsCodec",
      Sync(sync ArchitectureTestsMuxToolCore.muxHostToolsFuzzyUsesFuzzyToolsCodec)
      "ArchitectureTests.opencodeSearchToolsUsesFuzzyToolsCodec",
      Sync(sync ArchitectureTestsOpencodeToolsSearch.opencodeSearchToolsUsesFuzzyToolsCodec)
      "ArchitectureTests.fuzzyToolsCodecExists", Sync(sync ArchitectureTestsMuxToolCore.fuzzyToolsCodecExists)
      "ArchitectureTests.webToolsUsesWebfetchCodec", Sync(sync ArchitectureTestsMuxToolCore.webToolsUsesWebfetchCodec)
      "ArchitectureTests.opencodeSearchToolsUsesWebToolsCodec",
      Sync(sync ArchitectureTestsOpencodeToolsSearch.opencodeSearchToolsUsesWebToolsCodec)
      "ArchitectureTests.opencodeSearchToolsUsesToolCopy",
      Sync(sync ArchitectureTestsOpencodeToolsSearch.opencodeSearchToolsUsesToolCopy)
      "ArchitectureTests.opencodeExecutorUsesToolCopy",
      Sync(sync ArchitectureTestsOpencodeToolsExecutor.opencodeExecutorUsesToolCopy)
      "ArchitectureTests.opencodeExecutorUsesFromOpencode",
      Sync(sync ArchitectureTestsOpencodeToolsExecutor.opencodeExecutorUsesFromOpencode)
      "ArchitectureTests.opencodeExecutorUsesExecutorToolsCodec",
      Sync(sync ArchitectureTestsOpencodeToolsExecutor.opencodeExecutorUsesExecutorToolsCodec)
      "ArchitectureTests.opencodePluginCoreUsesFromOpencode",
      Sync(sync ArchitectureTestsWireHook.opencodePluginCoreUsesFromOpencode)
      "ArchitectureTests.opencodeHookExecuteUsesFromOpencode",
      Sync(sync ArchitectureTestsWireHook.opencodeHookExecuteUsesFromOpencode)
      "ArchitectureTests.opencodeChatHooksUsesHookInputCodec",
      Sync(sync ArchitectureTestsWireHook.opencodeChatHooksUsesHookInputCodec)
      "ArchitectureTests.chatHooksUsesChatHookOutputCodec",
      Sync(sync ArchitectureTestsWireHook.chatHooksUsesChatHookOutputCodec)
      "ArchitectureTests.opencodeMessageTransformUsesHookInputCodec",
      Sync(sync ArchitectureTestsWirePipeline.opencodeMessageTransformUsesHookInputCodec)
      "ArchitectureTests.opencodeMessageTransformUsesResolveMessagesTransformAgent",
      Sync(sync ArchitectureTestsWirePipeline.opencodeMessageTransformUsesResolveMessagesTransformAgent)
      "ArchitectureTests.opencodeCommandHooksUsesFromOpencode",
      Sync(sync ArchitectureTestsWireHook.opencodeCommandHooksUsesFromOpencode)
      "ArchitectureTests.opencodeSessionLifecycleObserverUsesHookInputCodec",
      Sync(sync ArchitectureTestsWireHook.opencodeSessionLifecycleObserverUsesHookInputCodec)
      "ArchitectureTests.opencodeEventHooksUsesEventEnvelopeCodec",
      Sync(sync ArchitectureTestsWireHook.opencodeEventHooksUsesEventEnvelopeCodec)
      "ArchitectureTests.opencodeToolDefinitionHooksUsesHookInputCodec",
      Sync(sync ArchitectureTestsWireHook.opencodeToolDefinitionHooksUsesHookInputCodec)
      "ArchitectureTests.muxHostToolsReadWriteUsesToolCatalog",
      Sync(sync ArchitectureTestsMuxToolCore.muxHostToolsReadWriteUsesToolCatalog)
      "ArchitectureTests.muxHostToolsReadWriteUsesFileToolsCodec",
      Sync(sync ArchitectureTestsMuxToolCore.muxHostToolsReadWriteUsesFileToolsCodec)
      "ArchitectureTests.muxWrappersTodoUsesWorkBacklogToolsCodec",
      Sync(sync ArchitectureTestsMuxToolAux.muxWrappersTodoUsesWorkBacklogToolsCodec)
      "ArchitectureTests.opencodeHookExecuteUsesPatchToolsCodec",
      Sync(sync ArchitectureTestsWireHook.opencodeHookExecuteUsesPatchToolsCodec)
      "ArchitectureTests.shellCodecFilesNoLocalStrField",
      Sync(sync ArchitectureTestsWireToolExec.shellCodecFilesNoLocalStrField)
      "ArchitectureTests.shellNonCodecMustUseDynFieldHelpers",
      Sync(sync ArchitectureTestsWireToolExec.shellNonCodecMustUseDynFieldHelpers)
      "ArchitectureTests.mustUseCodecHelper", Sync(sync ArchitectureTestsWireToolExec.mustUseCodecHelper)
      "ArchitectureTests.muxFileReadWrapperReturnsDisabled",
      Sync(sync ArchitectureTestsWireToolExec.muxFileReadWrapperReturnsDisabled)
      "ArchitectureTests.muxHostToolsExecutorUsesFromMuxConfig",
      Sync(sync ArchitectureTestsMuxToolCore.muxHostToolsExecutorUsesFromMuxConfig)
      "ArchitectureTests.muxHostToolsExecutorUsesExecutorToolsCodec",
      Sync(sync ArchitectureTestsMuxToolCore.muxHostToolsExecutorUsesExecutorToolsCodec)
      "ArchitectureTests.muxHostToolsWireDecodeFailures",
      Sync(sync ArchitectureTestsWireToolExec.muxHostToolsWireDecodeFailures)
      "ArchitectureTests.muxWebToolsUsesWireDecodeFailure",
      Sync(sync ArchitectureTestsWireToolExec.muxWebToolsUsesWireDecodeFailure)
      "ArchitectureTests.kernelToolCopyWebExecutorFields",
      Sync(sync ArchitectureTestsWireToolExec.kernelToolCopyWebExecutorFields)
      "ArchitectureTests.sessionExecutorCreateForScope",
      Sync(sync ArchitectureTestsRuntimeSession.sessionExecutorCreateForScope)
      "ArchitectureTests.pluginInjectsSessionScopeForExecutor",
      Sync(sync ArchitectureTestsRuntimeSession.pluginInjectsSessionScopeForExecutor)
      "ArchitectureTests.runtimeScopeNoGetDefault", Sync(sync ArchitectureTestsRuntimeSession.runtimeScopeNoGetDefault)
      "ArchitectureTests.sessionExecutorNoModuleMutableQueues",
      Sync(sync ArchitectureTestsRuntimeSession.sessionExecutorNoModuleMutableQueues)
      "ArchitectureTests.webToolsUsesWebToolsCodec", Sync(sync ArchitectureTestsMuxToolCore.webToolsUsesWebToolsCodec)
      "ArchitectureTests.dualHostFuzzyUsesFuzzyToolsCodec",
      Sync(sync ArchitectureTestsMuxToolCore.dualHostFuzzyUsesFuzzyToolsCodec)
      "ArchitectureTests.opencodeToolsUseWireEncodeForClient",
      Sync(sync ArchitectureTestsWireToolExec.opencodeToolsUseWireEncodeForClient)
      "ArchitectureTests.toolExecuteWireHelperExists",
      Sync(sync ArchitectureTestsWireToolExec.toolExecuteWireHelperExists)
      "ArchitectureTests.opencodeToolSchemaDescriptionsFromCatalog",
      Sync(sync ArchitectureTestsOpencodeTools.opencodeToolSchemaDescriptionsFromCatalog)
      "ArchitectureTests.opencodeToolsUseHostForSummarizerPrompts",
      Sync(sync ArchitectureTestsOpencodeTools.opencodeToolsUseHostForSummarizerPrompts)
      "ArchitectureTests.opencodeSessionEventCodecExists",
      Sync(sync ArchitectureTestsWirePayload.opencodeSessionEventCodecExists)
      "ArchitectureTests.nudgeEffectRecoversViaCodec",
      Sync(sync ArchitectureTestsWirePayload.nudgeEffectRecoversViaCodec)
      "ArchitectureTests.sessionLifecycleObserverUsesCodecDecoders",
      Sync(sync ArchitectureTestsWirePayload.sessionLifecycleObserverUsesCodecDecoders)
      "ArchitectureTests.commandHooksUsesCodecSessionID",
      Sync(sync ArchitectureTestsWirePayload.commandHooksUsesCodecSessionID)
      "ArchitectureTests.eventHooksUsesCodecSessionID",
      Sync(sync ArchitectureTestsWirePayload.eventHooksUsesCodecSessionID)
      "ArchitectureTests.e2eHarnessGetMessagesUsesSessionPrefix",
      Sync(sync E2eHarnessContractTests.getMessagesUsesSessionPrefix)
      "ArchitectureTests.e2eHarnessPluginJsResolvesWithParentFallback",
      Sync(sync E2eHarnessContractTests.pluginJsResolvesWithParentFallback)
      "ArchitectureTests.testFilesAreTimeIndependent",
      Sync(sync ArchitectureTestsTimeIndependence.testFilesAreTimeIndependent)
      "ArchitectureTests.wanxiangzhenBoundary", Sync(sync ArchitectureTestsFoundationB.wanxiangzhenBoundary)
      "ArchitectureTests.wanxiangzhenGitQueue", Sync(sync ArchitectureTestsFoundationB.wanxiangzhenGitQueue)
      "ArchitectureTests.wanxiangzhenReconcile", Sync(sync ArchitectureTestsFoundationB.wanxiangzhenReconcile)
      "ArchitectureTests.squadEventFoldUsesTransitionPolicy",
      Sync(sync ArchitectureTestsFoundationB.squadEventFoldUsesTransitionPolicy)
      "ArchitectureTests.reviewLoopFoldAdt", Sync(sync ArchitectureTestsFoundationB.reviewLoopFoldAdt)
      "ArchitectureTests.coordinatorReplayUsesTransitionPolicy",
      Sync(sync ArchitectureTestsFoundationB.coordinatorReplayUsesTransitionPolicy) ]
