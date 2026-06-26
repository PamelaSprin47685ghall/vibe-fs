module Wanxiangshu.Tests.TestsEntriesOmp

open Wanxiangshu.Tests.TestsTestBody
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
open Wanxiangshu.Tests.OmpKnowledgeGraphRuntimeTests
open Wanxiangshu.Tests.OmpSessionLifecycleTests
open Wanxiangshu.Tests.OmpPluginCoreTests
open Wanxiangshu.Tests.OmpTitleFetchGuardTests
open Wanxiangshu.Tests.OmpMagicTodoTests
open Wanxiangshu.Tests.OmpToolResultEventTests
open Wanxiangshu.Tests.OmpPluginCoreIntegrationTests
open Wanxiangshu.Tests.SubagentIoTests
open Wanxiangshu.Tests.OmpSessionCompactingTests

let ompTestEntries () : (string * TestBody) list =
    [
    "OmpKernelTests.filterOmpMainSessionTools", Sync (sync OmpKernelTests.filterOmpMainSessionTools)
    "OmpKernelTests.validateFetchUrlBlocksPrivate", Sync (sync OmpKernelTests.validateFetchUrlBlocksPrivate)
    "OmpKernelTests.reviewInstructionsCanonicalVerdictTool", Sync (sync OmpKernelTests.reviewInstructionsCanonicalVerdictTool)
    "OmpSessionToolsTests.mainSessionStripsChildOnlyAndBash", Sync (sync OmpSessionToolsTests.mainSessionStripsChildOnlyAndBash)
    "OmpSessionToolsTests.childSessionKeepsChildTools", Sync (sync OmpSessionToolsTests.childSessionKeepsChildTools)
    "OmpWebFetchTests.blocksLocalhostAndPrivateRanges", Sync (sync OmpWebFetchTests.blocksLocalhostAndPrivateRanges)
    "OmpWebFetchTests.rejectsUnsupportedScheme", Sync (sync OmpWebFetchTests.rejectsUnsupportedScheme)
    "OmpCapsTests.buildCapsFromUppercaseFiles", Async OmpCapsTests.buildCapsFromUppercaseFiles
    "OmpCapsTests.stripHostDirContext", Sync (sync OmpCapsTests.stripHostDirContext)
    "OmpCapsTests.appendCapsIdempotent", Async OmpCapsTests.appendCapsIdempotent
    "OmpCapsTests.capsSkipsExcludedDirs", Async OmpCapsTests.capsSkipsExcludedDirs
    "OmpCapsTests.capsRespectsFileCountBudget", Async OmpCapsTests.capsRespectsFileCountBudget
    "OmpFuzzyTests.fuzzyFindIteratorSingleUse", Sync (sync OmpFuzzyTests.fuzzyFindIteratorSingleUse)
    "OmpFuzzyTests.fuzzyGrepIteratorSingleUse", Sync (sync OmpFuzzyTests.fuzzyGrepIteratorSingleUse)
    "OmpFuzzyTests.registeredFuzzyToolsExposeIteratorParam", Async OmpFuzzyTests.registeredFuzzyToolsExposeIteratorParam
    "OmpKernelTests.executorSummarizerPromptCarriesWhatToSummarize", Sync (sync OmpKernelTests.executorSummarizerPromptCarriesWhatToSummarize)
    "OmpPluginTests.registersCoreToolsIdempotent", Async OmpPluginTests.registersCoreToolsIdempotent
    "OmpPluginTests.methodologySchemaCarriesMinItems", Async OmpPluginTests.methodologySchemaCarriesMinItems
    "OmpPluginTests.sessionStartStripsMainSessionTools", Async OmpPluginTests.sessionStartStripsMainSessionTools
    "OmpPluginTests.fuzzyDescriptionsMatchMuxWording", Sync (sync OmpPluginTests.fuzzyDescriptionsMatchMuxWording)
    "OmpContextTransformTests.capsSynthUserPrepended", Async OmpContextTransformTests.capsSynthUserPrepended
    "OmpContextTransformTests.capsReadToolsInContextTransform", Async OmpContextTransformTests.capsReadToolsInContextTransform
    "OmpContextTransformTests.beforeAgentStartOmitsCapsXml", Async OmpContextTransformTests.beforeAgentStartOmitsCapsXml
    "OmpContextTransformTests.knowledgeGraphPreludeWhenKgPresent", Async OmpContextTransformTests.knowledgeGraphPreludeWhenKgPresent
    "OmpContextTransformTests.reviewReplayIfStoreEmptyOnTransform", Async OmpContextTransformTests.reviewReplayIfStoreEmptyOnTransform
    "OmpPluginTests.readAssistantTextFromEntries", Sync (sync OmpPluginTests.readAssistantTextFromEntries)
    "OmpPluginTests.subagentPromptsContainKernelFragments", Sync (sync OmpPluginTests.subagentPromptsContainKernelFragments)
    "OmpPluginTests.executorToolSchemaFourFields", Async OmpPluginTests.executorToolSchemaFourFields
    "OmpPluginTests.browserErrorsWithoutBrowserHost", Async OmpPluginTests.browserErrorsWithoutBrowserHost
    "OmpPluginTests.reviewChildInitialPromptUsesReturnReviewer", Sync (sync OmpPluginTests.reviewChildInitialPromptUsesReturnReviewer)
    "OmpPluginTests.fuzzyGrepExcludeAnyOfLength2", Async OmpPluginTests.fuzzyGrepExcludeAnyOfLength2
    "OmpPluginTestsAgentEnd.agentEndRunnerNudgeBeforeLoop", Async OmpPluginTestsAgentEnd.agentEndRunnerNudgeBeforeLoop
    "OmpPluginTestsAgentEnd.agentEndLoopNudgeWhenActive", Async OmpPluginTestsAgentEnd.agentEndLoopNudgeWhenActive
    "OmpPluginTestsAgentEnd.agentEndSkipsLoopNudgeWhenPendingMessages", Async OmpPluginTestsAgentEnd.agentEndSkipsLoopNudgeWhenPendingMessages
    "OmpPluginTestsAgentEnd.agentEndTodoNudgeWhenOpenPhases", Async OmpPluginTestsAgentEnd.agentEndTodoNudgeWhenOpenPhases
    "OmpPluginTestsAgentEnd.runnerNudgePromptUsesExecutorToolNames", Sync (sync OmpPluginTestsAgentEnd.runnerNudgePromptUsesExecutorToolNames)
    "OmpReviewTests.returnReviewerVerdictPassReject", Async OmpReviewTests.returnReviewerVerdictPassReject
    "OmpReviewTests.returnReviewerViaSetPendingStateForTest", Async OmpReviewTests.returnReviewerViaSetPendingStateForTest
    "OmpReviewTests.runReviewLoopChildToolNames", Async OmpReviewTests.runReviewLoopChildToolNames
    "OmpReviewTests.runReviewLoopAcceptsWhenPendingResolved", Async OmpReviewTests.runReviewLoopAcceptsWhenPendingResolved
    "OmpChildSessionTests.createChildSessionReviewToolNames", Async OmpChildSessionTests.createChildSessionReviewToolNames
    "OmpChildSessionTests.createChildSessionRunnerToolNames", Async OmpChildSessionTests.createChildSessionRunnerToolNames
    "OmpRunnerTests.waitRunnerJobAfterAppendLog", Async OmpRunnerTests.waitRunnerJobAfterAppendLog
    "OmpRunnerTests.setRunnerJobStateForTestHasRunning", Sync (sync OmpRunnerTests.setRunnerJobStateForTestHasRunning)
    "OmpRunnerTests.abortRunnerJobClearsRunning", Sync (sync OmpRunnerTests.abortRunnerJobClearsRunning)
    "OmpRunnerTests.cleanupRunnerJobClearsRunning", Async OmpRunnerTests.cleanupRunnerJobClearsRunning
    "OmpRunnerTests.hasRunningWhenActiveExecutorRun", Sync (sync OmpRunnerTests.hasRunningWhenActiveExecutorRun)
    "OmpRunnerTests.abortExecutorRunClearsActive", Sync (sync OmpRunnerTests.abortExecutorRunClearsActive)
    "OmpRunnerTests.executorChildToolNamesMatchOmpSessionTools", Sync (sync OmpRunnerTests.executorChildToolNamesMatchOmpSessionTools)
    "OmpHelpersTests.checkSyntaxBadJson", Async OmpHelpersTests.checkSyntaxBadJson
    "OmpHelpersTests.checkSyntaxValidJson", Async OmpHelpersTests.checkSyntaxValidJson
    "OmpHelpersTests.checkSyntaxBrokenJsonReports_intentionalWarningFork", Async OmpHelpersTests.checkSyntaxBrokenJsonReports_intentionalWarningFork
    "OmpHelpersTests.supportsSyntaxDiagnosticsFileEditTools", Async OmpHelpersTests.supportsSyntaxDiagnosticsFileEditTools
    "OmpHelpersTests.supportsSyntaxDiagnosticsGrepFalse", Sync (sync OmpHelpersTests.supportsSyntaxDiagnosticsGrepFalse)
    "OmpHelpersTests.stripHeadTailViaKernel", Sync (sync OmpHelpersTests.stripHeadTailViaKernel)
    "OmpHelpersTests.stripHeadTailChain", Sync (sync OmpHelpersTests.stripHeadTailChain)
    "OmpHelpersTests.getOllamaApiKeyFromEnv", Sync (sync OmpHelpersTests.getOllamaApiKeyFromEnv)
    "OmpHelpersTests.getOllamaApiKeyMissingWhenUnset", Sync (sync OmpHelpersTests.getOllamaApiKeyMissingWhenUnset)
    "OmpHelpersTests.fuzzyGrepCursorSingleUse", Sync (sync OmpHelpersTests.fuzzyGrepCursorSingleUse)
    "OmpHelpersTests.fuzzyFindCursorSingleUse", Sync (sync OmpHelpersTests.fuzzyFindCursorSingleUse)
    "OmpHelpersTests.fuzzyResolveExternalBasePath", Sync (sync OmpHelpersTests.fuzzyResolveExternalBasePath)
    "OmpTitleFetchGuardTests.signature", Sync (sync OmpTitleFetchGuardTests.signature)
    "OmpTitleFetchGuardTests.wrapText", Sync (sync OmpTitleFetchGuardTests.wrapText)
    "OmpTitleFetchGuardTests.detectProbeUserContent", Sync (sync OmpTitleFetchGuardTests.detectProbeUserContent)
    "OmpTitleFetchGuardTests.rejectNonProbeBody", Sync (sync OmpTitleFetchGuardTests.rejectNonProbeBody)
    "OmpTitleFetchGuardTests.rejectNonJsonBody", Sync (sync OmpTitleFetchGuardTests.rejectNonJsonBody)
    "OmpTitleFetchGuardTests.rewriteStringContent", Sync (sync OmpTitleFetchGuardTests.rewriteStringContent)
    "OmpTitleFetchGuardTests.rewriteArrayContent", Sync (sync OmpTitleFetchGuardTests.rewriteArrayContent)
    "SubagentIoTests.firstStringPreferListed", Sync (sync SubagentIoTests.firstStringPreferListed)
    "SubagentIoTests.extractToolContextDirectoryFallback", Sync (sync SubagentIoTests.extractToolContextDirectoryFallback)
    "SubagentIoTests.extractToolContextHonoursCtx", Sync (sync SubagentIoTests.extractToolContextHonoursCtx)
    "SubagentIoTests.textPartsWrapsStrings", Sync (sync SubagentIoTests.textPartsWrapsStrings)
    "SubagentIoTests.buildPromptBodyNoAiSettings", Sync (sync SubagentIoTests.buildPromptBodyNoAiSettings)
    "SubagentIoTests.buildPromptBodyWithThinkingLevel", Sync (sync SubagentIoTests.buildPromptBodyWithThinkingLevel)
    "SubagentIoTests.signalAbortedFalseOnNull", Sync (sync SubagentIoTests.signalAbortedFalseOnNull)
    "OmpPluginCoreTests.reviewStoreIsSharedSingleton", Sync (sync OmpPluginCoreTests.reviewStoreIsSharedSingleton)
    "OmpPluginCoreTests.clearReviewStatesNoError", Sync (sync OmpPluginCoreTests.clearReviewStatesNoError)
    "OmpPluginCoreTests.abortHookDeactivatesReview", Sync (sync OmpPluginCoreTests.abortHookDeactivatesReview)
    "OmpPluginCoreTests.streamAbortHookDeactivatesReview", Sync (sync OmpPluginCoreTests.streamAbortHookDeactivatesReview)
    "OmpPluginCoreTests.sessionErrorHookDeactivatesReview", Sync (sync OmpPluginCoreTests.sessionErrorHookDeactivatesReview)
    "OmpPluginCoreTests.unrelatedEventLeavesReviewActive", Sync (sync OmpPluginCoreTests.unrelatedEventLeavesReviewActive)
    "OmpMagicTodoTests.sharedSessionStoreByHost", Sync (sync OmpMagicTodoTests.sharedSessionStoreByHost)
    "OmpMagicTodoTests.hostPartitionedReports", Sync (sync OmpMagicTodoTests.hostPartitionedReports)
    "OmpMagicTodoTests.backlogReportFromTodoInputHostAgnostic", Sync (sync OmpMagicTodoTests.backlogReportFromTodoInputHostAgnostic)
    "OmpMagicTodoTests.inputOfPartNonTool", Sync (sync OmpMagicTodoTests.inputOfPartNonTool)
    "OmpMagicTodoTests.replayBacklogOmpFallsBackToCapturedReport", Sync (sync OmpMagicTodoTests.replayBacklogOmpFallsBackToCapturedReport)
    "OmpToolResultEventTests.getToolInputPrefersInputOverArgs", Sync (sync OmpToolResultEventTests.getToolInputPrefersInputOverArgs)
    "OmpToolResultEventTests.getToolInputFallsBackToArgs", Sync (sync OmpToolResultEventTests.getToolInputFallsBackToArgs)
    "OmpToolResultEventTests.getToolInputReturnsNullishWhenNeitherPresent", Sync (sync OmpToolResultEventTests.getToolInputReturnsNullishWhenNeitherPresent)
    "OmpToolResultEventTests.getToolCallIdPrefersToolCallId", Sync (sync OmpToolResultEventTests.getToolCallIdPrefersToolCallId)
    "OmpToolResultEventTests.getToolCallIdFallsBackToCallId", Sync (sync OmpToolResultEventTests.getToolCallIdFallsBackToCallId)
    "OmpToolResultEventTests.getToolCallIdFallsBackToCallID", Sync (sync OmpToolResultEventTests.getToolCallIdFallsBackToCallID)
    "OmpToolResultEventTests.getToolCallIdReturnsEmptyWhenNonePresent", Sync (sync OmpToolResultEventTests.getToolCallIdReturnsEmptyWhenNonePresent)
    "OmpToolResultEventTests.getToolResultTextFromContentArray", Sync (sync OmpToolResultEventTests.getToolResultTextFromContentArray)
    "OmpToolResultEventTests.getToolResultTextFromContentArrayMixed", Sync (sync OmpToolResultEventTests.getToolResultTextFromContentArrayMixed)
    "OmpToolResultEventTests.getToolResultTextFromStringContent", Sync (sync OmpToolResultEventTests.getToolResultTextFromStringContent)
    "OmpToolResultEventTests.setToolResultTextLeavesReadableText", Sync (sync OmpToolResultEventTests.setToolResultTextLeavesReadableText)
    "OmpToolResultEventTests.setToolResultTextPreservesArrayForm", Sync (sync OmpToolResultEventTests.setToolResultTextPreservesArrayForm)
    "OmpToolResultEventTests.setToolResultTextContentIsStringAfterWrite", Sync (sync OmpToolResultEventTests.setToolResultTextContentIsStringAfterWrite)
    "OmpPluginCoreIntegrationTests.extensionIsIdempotent", Async OmpPluginCoreIntegrationTests.extensionIsIdempotent
    "OmpPluginCoreIntegrationTests.extensionRegistersLifecycleHooks", Async OmpPluginCoreIntegrationTests.extensionRegistersLifecycleHooks
    "OmpPluginCoreIntegrationTests.reviewStoreSharedWithTools", Async OmpPluginCoreIntegrationTests.reviewStoreSharedWithTools
    "OmpAgentConfigTests.applyAgentConfigForRegistersBuiltinAgents", Sync (sync OmpAgentConfigTests.applyAgentConfigForRegistersBuiltinAgents)
    "OmpAgentConfigTests.applyAgentConfigForPreservesUserOverrides", Sync (sync OmpAgentConfigTests.applyAgentConfigForPreservesUserOverrides)
    "OmpAgentConfigTests.disableNativeAgentsClearsMemoryAndCheckpoint", Sync (sync OmpAgentConfigTests.disableNativeAgentsClearsMemoryAndCheckpoint)
    "OmpAgentConfigTests.disableNativeAgentsPreservesUserOverrides", Sync (sync OmpAgentConfigTests.disableNativeAgentsPreservesUserOverrides)
    "OmpAgentConfigTests.applyAgentConfigForPreservesUserPermissionAndMcps", Sync (sync OmpAgentConfigTests.applyAgentConfigForPreservesUserPermissionAndMcps)
    "OmpAgentConfigTests.applyAgentConfigForKeepsUserCustomAgents", Sync (sync OmpAgentConfigTests.applyAgentConfigForKeepsUserCustomAgents)
    "OmpAgentConfigTests.disableNativeAgentsReplacesCheckpointSection", Sync (sync OmpAgentConfigTests.disableNativeAgentsReplacesCheckpointSection)
    "OmpHookExecuteTests.hookCoderInjectUiLabel", Sync (sync OmpHookExecuteTests.hookCoderInjectUiLabel)
    "OmpHookExecuteTests.hookInvestigatorInjectUiLabel", Sync (sync OmpHookExecuteTests.hookInvestigatorInjectUiLabel)
    "OmpHookExecuteTests.hookNonSubagentDoesNotInjectUiLabel", Sync (sync OmpHookExecuteTests.hookNonSubagentDoesNotInjectUiLabel)
    "OmpHookExecuteTests.hookApplyPatchNormalisesPatchToPatchText", Sync (sync OmpHookExecuteTests.hookApplyPatchNormalisesPatchToPatchText)
    "OmpHookExecuteTests.hookApplyPatchStringArgsIsNoOp", Sync (sync OmpHookExecuteTests.hookApplyPatchStringArgsIsNoOp)
    "OmpHookExecuteTests.hookPatchNameNormalisesToPatchText", Sync (sync OmpHookExecuteTests.hookPatchNameNormalisesToPatchText)
    "OmpHookExecuteTests.hookApplyPatchLeavesExistingPatchTextUntouched", Sync (sync OmpHookExecuteTests.hookApplyPatchLeavesExistingPatchTextUntouched)
    "OmpSessionLifecycleTests.recordsToBookkeeperIncludesApplyPatch", Sync (sync OmpSessionLifecycleTests.recordsToBookkeeperIncludesApplyPatch)
    "OmpSessionLifecycleTests.isReadOnlyExecutorTrueForRoMode", Sync (sync OmpSessionLifecycleTests.isReadOnlyExecutorTrueForRoMode)
    "OmpSessionLifecycleTests.isReadOnlyExecutorFalseForRwMode", Sync (sync OmpSessionLifecycleTests.isReadOnlyExecutorFalseForRwMode)
    "OmpSessionLifecycleTests.isChildSessionGuardSkipsBookkeeper", Sync (sync OmpSessionLifecycleTests.isChildSessionGuardSkipsBookkeeper)
    "OmpKnowledgeGraphRuntimeTests.submitRejectsWhenKgDirMissing", Async OmpKnowledgeGraphRuntimeTests.submitRejectsWhenKgDirMissing
    "OmpKnowledgeGraphRuntimeTests.submitRoutesByWorkspaceRoot", Sync (sync OmpKnowledgeGraphRuntimeTests.submitRoutesByWorkspaceRoot)
    "OmpKnowledgeGraphRuntimeTests.submitKeepsTwoSessionsPerRootDistinct", Sync (sync OmpKnowledgeGraphRuntimeTests.submitKeepsTwoSessionsPerRootDistinct)
    "OmpKnowledgeGraphRuntimeTests.takeBookkeeperLaunchesForTestingStartsEmpty", Sync (sync OmpKnowledgeGraphRuntimeTests.takeBookkeeperLaunchesForTestingStartsEmpty)
    "OmpKnowledgeGraphRuntimeTests.startMaintenanceIfDueNoopsForBlankRoot", Async OmpKnowledgeGraphRuntimeTests.startMaintenanceIfDueNoopsForBlankRoot
    "OmpSessionCompactingTests.sessionCompactingHandlerEmptyMessages", Async OmpSessionCompactingTests.sessionCompactingHandlerEmptyMessages
    "OmpSessionCompactingTests.sessionCompactingHandlerNullMessages", Async OmpSessionCompactingTests.sessionCompactingHandlerNullMessages
    "OmpSessionCompactingTests.sessionCompactingHandlerWithMessages", Async OmpSessionCompactingTests.sessionCompactingHandlerWithMessages
    "OmpSessionCompactingTests.sessionCompactingPreservesCompletedWorkReport", Async OmpSessionCompactingTests.sessionCompactingPreservesCompletedWorkReport
    "OmpSessionCompactingTests.sessionCompactingStripsSynthetic", Async OmpSessionCompactingTests.sessionCompactingStripsSynthetic
    "OmpPluginTests.extensionRegistersLifecycleHooks", Async OmpPluginTests.extensionRegistersLifecycleHooks
    "OmpPluginTests.toolCallHookCanBeInvoked", Async OmpPluginTests.toolCallHookCanBeInvoked
    "OmpPluginTests.sessionCompactingHookCanBeInvoked", Async OmpPluginTests.sessionCompactingHookCanBeInvoked
    "OmpPluginTests.toolCallBlocksChildOnlyInMainSession", Async OmpPluginTests.toolCallBlocksChildOnlyInMainSession
    "OmpPluginTests.turnStartRestoresMainSessionTools", Async OmpPluginTests.turnStartRestoresMainSessionTools
    ]