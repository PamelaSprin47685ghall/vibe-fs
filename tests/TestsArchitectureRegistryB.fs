module Wanxiangshu.Tests.TestsArchitectureRegistryB

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsFoundation
open Wanxiangshu.Tests.ArchitectureTestsMessageTransform
open Wanxiangshu.Tests.ArchitectureTestsSubagent
open Wanxiangshu.Tests.ArchitectureTestsSubagentCatalog
open Wanxiangshu.Tests.ArchitectureTestsSubagentSession
open Wanxiangshu.Tests.ArchitectureTestsSubagentToolExec
open Wanxiangshu.Tests.ArchitectureTestsRuntime
open Wanxiangshu.Tests.ArchitectureTestsRuntimeSession
open Wanxiangshu.Tests.ArchitectureTestsRuntimeKg
open Wanxiangshu.Tests.ArchitectureTestsWireToolExec
open Wanxiangshu.Tests.ArchitectureTestsWireHook
open Wanxiangshu.Tests.ArchitectureTestsWireHookMux
open Wanxiangshu.Tests.ArchitectureTestsWirePipeline
open Wanxiangshu.Tests.ArchitectureTestsWirePayload
open Wanxiangshu.Tests.ArchitectureTestsMuxToolCore
open Wanxiangshu.Tests.ArchitectureTestsMuxToolAux
open Wanxiangshu.Tests.ArchitectureTestsMuxToolAuxKg
open Wanxiangshu.Tests.ArchitectureTestsOpencodeTools
open Wanxiangshu.Tests.ArchitectureTestsOpencodeToolsReview
open Wanxiangshu.Tests.ArchitectureTestsOpencodeToolsSearch
open Wanxiangshu.Tests.ArchitectureTestsOpencodeToolsExecutor
open Wanxiangshu.Tests.TestsTestBody

let architectureTestEntriesPartB () : (string * TestBody) list =
    [
    "ArchitectureTests.messageTransformUsesCapsKgHostHooks", Sync (sync ArchitectureTestsWirePipeline.messageTransformUsesCapsKgHostHooks)
    "ArchitectureTests.messageTransformUsesBacklogSessionOpsFrom", Sync (sync ArchitectureTestsWirePipeline.messageTransformUsesBacklogSessionOpsFrom)
    "ArchitectureTests.knowledgeGraphRuntimeUsesWorkflow", Sync (sync ArchitectureTestsRuntimeKg.knowledgeGraphRuntimeUsesWorkflow)
    "ArchitectureTests.knowledgeGraphBookkeeperLaunchInShell", Sync (sync ArchitectureTestsRuntimeKg.knowledgeGraphBookkeeperLaunchInShell)
    "ArchitectureTests.knowledgeGraphRuntimeNoLocalLaunchIfDue", Sync (sync ArchitectureTestsRuntimeKg.knowledgeGraphRuntimeNoLocalLaunchIfDue)
    "ArchitectureTests.muxReviewUsesToolCopy", Sync (sync ArchitectureTestsMuxToolAux.muxReviewUsesToolCopy)
    "ArchitectureTests.muxReviewUsesFromMuxConfig", Sync (sync ArchitectureTestsMuxToolAux.muxReviewUsesFromMuxConfig)
    "ArchitectureTests.muxReviewUsesReviewToolsCodec", Sync (sync ArchitectureTestsMuxToolAux.muxReviewUsesReviewToolsCodec)
    "ArchitectureTests.executeMuxSubagentToolUsesSpawnRoleOnly", Sync (sync ArchitectureTestsSubagentToolExec.executeMuxSubagentToolUsesSpawnRoleOnly)
    "ArchitectureTests.subagentToolExecuteEmptyBatchGuard", Sync (sync ArchitectureTestsSubagentToolExec.subagentToolExecuteEmptyBatchGuard)
    "ArchitectureTests.opencodeReviewUsesToolCopy", Sync (sync ArchitectureTestsOpencodeToolsReview.opencodeReviewUsesToolCopy)
    "ArchitectureTests.opencodeReviewUsesFromOpencode", Sync (sync ArchitectureTestsOpencodeToolsReview.opencodeReviewUsesFromOpencode)
    "ArchitectureTests.opencodeReviewUsesReviewToolsCodec", Sync (sync ArchitectureTestsOpencodeToolsReview.opencodeReviewUsesReviewToolsCodec)
    "ArchitectureTests.opencodeKgUsesKnowledgeGraphToolsCodec", Sync (sync ArchitectureTestsOpencodeTools.opencodeKgUsesKnowledgeGraphToolsCodec)
    "ArchitectureTests.muxKgToolDefsUsesKnowledgeGraphToolsCodec", Sync (sync ArchitectureTestsMuxToolAuxKg.muxKgToolDefsUsesKnowledgeGraphToolsCodec)
    "ArchitectureTests.muxKgToolDefsUsesFromMuxConfig", Sync (sync ArchitectureTestsMuxToolAuxKg.muxKgToolDefsUsesFromMuxConfig)
    "ArchitectureTests.opencodeSubagentToolsUsesSimpleArgsCodec", Sync (sync ArchitectureTestsSubagentSession.opencodeSubagentToolsUsesSimpleArgsCodec)
    "ArchitectureTests.muxSubagentToolsUsesSimpleArgsCodec", Sync (sync ArchitectureTestsMuxToolAux.muxSubagentToolsUsesSimpleArgsCodec)
    "ArchitectureTests.subagentToolsUseDecodeIntentsField", Sync (sync ArchitectureTestsSubagent.subagentToolsUseDecodeIntentsField)
    "ArchitectureTests.subagentToolsUseToolCatalogRequiredKeys", Sync (sync ArchitectureTestsSubagentCatalog.subagentToolsUseToolCatalogRequiredKeys)
    "ArchitectureTests.kernelToolArgsExists", Sync (sync ArchitectureTestsSubagentCatalog.kernelToolArgsExists)
    "ArchitectureTests.toolArgsDecodeExists", Sync (sync ArchitectureTestsSubagentCatalog.toolArgsDecodeExists)
    "ArchitectureTests.toolArgsDecodeCoversMajorTools", Sync (sync ArchitectureTestsSubagentCatalog.toolArgsDecodeCoversMajorTools)
    "ArchitectureTests.decodedToolInvocationNoObj", Sync (sync ArchitectureTestsSubagentCatalog.decodedToolInvocationNoObj)
    "ArchitectureTests.muxSubagentToolsUsesToolArgsDecode", Sync (sync ArchitectureTestsSubagentToolExec.muxSubagentToolsUsesToolArgsDecode)
    "ToolArgsDecodeTests.run", Sync (sync ToolArgsDecodeTests.run)
    "ToolResultWireTests.run", Sync (sync ToolResultWireTests.run)
    "SubagentToolExecuteTests.run", Async SubagentToolExecuteTests.run
    "ArchitectureTests.opencodeSubagentToolsUsesToolArgsDecode", Sync (sync ArchitectureTestsSubagentToolExec.opencodeSubagentToolsUsesToolArgsDecode)
    "ArchitectureTests.sessionIoRunSubagentReturnsResult", Sync (sync ArchitectureTestsSubagentSession.sessionIoRunSubagentReturnsResult)
    "ArchitectureTests.commandHooksUsesToolCopyReviewMessages", Sync (sync ArchitectureTestsSubagentSession.commandHooksUsesToolCopyReviewMessages)
    "ArchitectureTests.commandHooksUsesRegisterLoopReviewCommands", Sync (sync ArchitectureTestsWireHook.commandHooksUsesRegisterLoopReviewCommands)
    "ArchitectureTests.opencodeHookExecuteUsesHookArgsHelpers", Sync (sync ArchitectureTestsWireToolExec.opencodeHookExecuteUsesHookArgsHelpers)
    "ArchitectureTests.opencodeCommandHooksUsesPartsWriter", Sync (sync ArchitectureTestsWireToolExec.opencodeCommandHooksUsesPartsWriter)
    "ArchitectureTests.muxHookOutputUsesMuxHookInputCodec", Sync (sync ArchitectureTestsWireToolExec.muxHookOutputUsesMuxHookInputCodec)
    "ArchitectureTests.subagentToolsUseSubagentSpawn", Sync (sync ArchitectureTestsSubagentToolExec.subagentToolsUseSubagentSpawn)
    "ArchitectureTests.muxWrappersSyntaxUsesFromMuxConfig", Sync (sync ArchitectureTestsMuxToolAux.muxWrappersSyntaxUsesFromMuxConfig)
    "ArchitectureTests.muxHostToolsFuzzyUsesToolCopy", Sync (sync ArchitectureTestsMuxToolCore.muxHostToolsFuzzyUsesToolCopy)
    "ArchitectureTests.muxHostToolsFuzzyUsesFromMuxConfig", Sync (sync ArchitectureTestsMuxToolCore.muxHostToolsFuzzyUsesFromMuxConfig)
    "ArchitectureTests.muxHostToolsFuzzyUsesFuzzyToolsCodec", Sync (sync ArchitectureTestsMuxToolCore.muxHostToolsFuzzyUsesFuzzyToolsCodec)
    "ArchitectureTests.opencodeSearchToolsUsesFuzzyToolsCodec", Sync (sync ArchitectureTestsOpencodeToolsSearch.opencodeSearchToolsUsesFuzzyToolsCodec)
    "ArchitectureTests.fuzzyToolsCodecExists", Sync (sync ArchitectureTestsMuxToolCore.fuzzyToolsCodecExists)
    "ArchitectureTests.webToolsUsesWebfetchCodec", Sync (sync ArchitectureTestsMuxToolCore.webToolsUsesWebfetchCodec)
    "ArchitectureTests.opencodeSearchToolsUsesWebToolsCodec", Sync (sync ArchitectureTestsOpencodeToolsSearch.opencodeSearchToolsUsesWebToolsCodec)
    "ArchitectureTests.opencodeSearchToolsUsesToolCopy", Sync (sync ArchitectureTestsOpencodeToolsSearch.opencodeSearchToolsUsesToolCopy)
    "ArchitectureTests.opencodeExecutorUsesToolCopy", Sync (sync ArchitectureTestsOpencodeToolsExecutor.opencodeExecutorUsesToolCopy)
    "ArchitectureTests.opencodeExecutorUsesFromOpencode", Sync (sync ArchitectureTestsOpencodeToolsExecutor.opencodeExecutorUsesFromOpencode)
    "ArchitectureTests.opencodeExecutorUsesExecutorToolsCodec", Sync (sync ArchitectureTestsOpencodeToolsExecutor.opencodeExecutorUsesExecutorToolsCodec)
    "ArchitectureTests.opencodePluginCoreUsesFromOpencode", Sync (sync ArchitectureTestsWireHook.opencodePluginCoreUsesFromOpencode)
    "ArchitectureTests.opencodeHookExecuteUsesFromOpencode", Sync (sync ArchitectureTestsWireHook.opencodeHookExecuteUsesFromOpencode)
    "ArchitectureTests.opencodeChatHooksUsesHookInputCodec", Sync (sync ArchitectureTestsWireHook.opencodeChatHooksUsesHookInputCodec)
    "ArchitectureTests.chatHooksUsesChatHookOutputCodec", Sync (sync ArchitectureTestsWireHook.chatHooksUsesChatHookOutputCodec)
    "ArchitectureTests.opencodeMessageTransformUsesHookInputCodec", Sync (sync ArchitectureTestsWirePipeline.opencodeMessageTransformUsesHookInputCodec)
    "ArchitectureTests.opencodeMessageTransformUsesResolveMessagesTransformAgent", Sync (sync ArchitectureTestsWirePipeline.opencodeMessageTransformUsesResolveMessagesTransformAgent)
    "ArchitectureTests.opencodeCommandHooksUsesFromOpencode", Sync (sync ArchitectureTestsWireHook.opencodeCommandHooksUsesFromOpencode)
    "ArchitectureTests.opencodeSessionLifecycleObserverUsesHookInputCodec", Sync (sync ArchitectureTestsWireHook.opencodeSessionLifecycleObserverUsesHookInputCodec)
    "ArchitectureTests.opencodeEventHooksUsesEventEnvelopeCodec", Sync (sync ArchitectureTestsWireHook.opencodeEventHooksUsesEventEnvelopeCodec)
    "ArchitectureTests.opencodeToolDefinitionHooksUsesHookInputCodec", Sync (sync ArchitectureTestsWireHook.opencodeToolDefinitionHooksUsesHookInputCodec)
    "ArchitectureTests.opencodeKnowledgeGraphToolsUsesFromOpencode", Sync (sync ArchitectureTestsOpencodeTools.opencodeKnowledgeGraphToolsUsesFromOpencode)
    "ArchitectureTests.muxKnowledgeGraphStartBookkeeperUsesFromMuxConfig", Sync (sync ArchitectureTestsMuxToolAuxKg.muxKnowledgeGraphStartBookkeeperUsesFromMuxConfig)
    "ArchitectureTests.muxPluginRegistrationOrchestration", Sync (sync ArchitectureTestsMuxToolAuxKg.muxPluginRegistrationOrchestration)
    "ArchitectureTests.muxHostToolsReadWriteUsesToolCatalog", Sync (sync ArchitectureTestsMuxToolCore.muxHostToolsReadWriteUsesToolCatalog)
    "ArchitectureTests.muxHostToolsReadWriteUsesFileToolsCodec", Sync (sync ArchitectureTestsMuxToolCore.muxHostToolsReadWriteUsesFileToolsCodec)
    "ArchitectureTests.muxWrappersTodoUsesWorkBacklogToolsCodec", Sync (sync ArchitectureTestsMuxToolAux.muxWrappersTodoUsesWorkBacklogToolsCodec)
    "ArchitectureTests.opencodeHookExecuteUsesPatchToolsCodec", Sync (sync ArchitectureTestsWireHook.opencodeHookExecuteUsesPatchToolsCodec)
    "ArchitectureTests.shellCodecFilesNoLocalStrField", Sync (sync ArchitectureTestsWireToolExec.shellCodecFilesNoLocalStrField)
    "ArchitectureTests.shellNonCodecMustUseDynFieldHelpers", Sync (sync ArchitectureTestsWireToolExec.shellNonCodecMustUseDynFieldHelpers)
    "ArchitectureTests.mustUseCodecHelper", Sync (sync ArchitectureTestsWireToolExec.mustUseCodecHelper)
    "ArchitectureTests.muxFileReadWrapperReturnsDisabled", Sync (sync ArchitectureTestsWireToolExec.muxFileReadWrapperReturnsDisabled)
    "ArchitectureTests.muxDelegateUsesDelegateToolsCodec", Sync (sync ArchitectureTestsMuxToolAuxKg.muxDelegateUsesDelegateToolsCodec)
    "ArchitectureTests.muxHookInputCodecExecutorReadOnlyUsesCodec", Sync (sync ArchitectureTestsMuxToolAuxKg.muxHookInputCodecExecutorReadOnlyUsesCodec)
    "ArchitectureTests.knowledgeGraphSessionMessagesNotInRuntimeIO", Sync (sync ArchitectureTestsRuntimeKg.knowledgeGraphSessionMessagesNotInRuntimeIO)
    "ArchitectureTests.muxHostToolsExecutorUsesFromMuxConfig", Sync (sync ArchitectureTestsMuxToolCore.muxHostToolsExecutorUsesFromMuxConfig)
    "ArchitectureTests.muxHostToolsExecutorUsesExecutorToolsCodec", Sync (sync ArchitectureTestsMuxToolCore.muxHostToolsExecutorUsesExecutorToolsCodec)
    "ArchitectureTests.muxHostToolsWireDecodeFailures", Sync (sync ArchitectureTestsWireToolExec.muxHostToolsWireDecodeFailures)
    "ArchitectureTests.muxWebToolsUsesWireDecodeFailure", Sync (sync ArchitectureTestsWireToolExec.muxWebToolsUsesWireDecodeFailure)
    "ArchitectureTests.kernelToolCopyWebExecutorFields", Sync (sync ArchitectureTestsWireToolExec.kernelToolCopyWebExecutorFields)
    "ArchitectureTests.sessionExecutorCreateForScope", Sync (sync ArchitectureTestsRuntimeSession.sessionExecutorCreateForScope)
    "ArchitectureTests.pluginInjectsSessionScopeForExecutor", Sync (sync ArchitectureTestsRuntimeSession.pluginInjectsSessionScopeForExecutor)
    "ArchitectureTests.knowledgeGraphRuntimeNoTestDrainMembers", Sync (sync ArchitectureTestsRuntimeKg.knowledgeGraphRuntimeNoTestDrainMembers)
    "ArchitectureTests.knowledgeGraphRuntimeNoSwapStateMembers", Sync (sync ArchitectureTestsRuntimeKg.knowledgeGraphRuntimeNoSwapStateMembers)
    "ArchitectureTests.runtimeScopeNoGetDefault", Sync (sync ArchitectureTestsRuntimeSession.runtimeScopeNoGetDefault)
    "ArchitectureTests.sessionExecutorNoModuleMutableQueues", Sync (sync ArchitectureTestsRuntimeSession.sessionExecutorNoModuleMutableQueues)
    "ArchitectureTests.muxAiSettingsUsesMuxAiSettingsCodec", Sync (sync ArchitectureTestsMuxToolAuxKg.muxAiSettingsUsesMuxAiSettingsCodec)
    "ArchitectureTests.webToolsUsesWebToolsCodec", Sync (sync ArchitectureTestsMuxToolCore.webToolsUsesWebToolsCodec)
    "ArchitectureTests.dualHostFuzzyUsesFuzzyToolsCodec", Sync (sync ArchitectureTestsMuxToolCore.dualHostFuzzyUsesFuzzyToolsCodec)
    "ArchitectureTests.opencodeToolsUseWireEncodeForClient", Sync (sync ArchitectureTestsWireToolExec.opencodeToolsUseWireEncodeForClient)
    "ArchitectureTests.toolExecuteWireHelperExists", Sync (sync ArchitectureTestsWireToolExec.toolExecuteWireHelperExists)
    "ArchitectureTests.muxPluginToolExecuteAfterUsesMuxHookInputCodec", Sync (sync ArchitectureTestsWireHookMux.muxPluginToolExecuteAfterUsesMuxHookInputCodec)
    "ArchitectureTests.opencodeToolSchemaDescriptionsFromCatalog", Sync (sync ArchitectureTestsOpencodeTools.opencodeToolSchemaDescriptionsFromCatalog)
    "ArchitectureTests.opencodeToolsUseHostForSummarizerPrompts", Sync (sync ArchitectureTestsOpencodeTools.opencodeToolsUseHostForSummarizerPrompts)
    "ArchitectureTests.opencodeSessionEventCodecExists", Sync (sync ArchitectureTestsWirePayload.opencodeSessionEventCodecExists)
    "ArchitectureTests.nudgeEffectRecoversViaCodec", Sync (sync ArchitectureTestsWirePayload.nudgeEffectRecoversViaCodec)
    "ArchitectureTests.sessionLifecycleObserverUsesCodecDecoders", Sync (sync ArchitectureTestsWirePayload.sessionLifecycleObserverUsesCodecDecoders)
    "ArchitectureTests.commandHooksUsesCodecSessionID", Sync (sync ArchitectureTestsWirePayload.commandHooksUsesCodecSessionID)
    "ArchitectureTests.eventHooksUsesCodecSessionID", Sync (sync ArchitectureTestsWirePayload.eventHooksUsesCodecSessionID)
    ]
