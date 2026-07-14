module Wanxiangshu.Tests.TestsEntriesOmp

open Wanxiangshu.Tests.TestsTestBody
open Wanxiangshu.Tests.OmpKernelTests
open Wanxiangshu.Tests.OmpSessionToolsTests
open Wanxiangshu.Tests.OmpWebFetchTests
open Wanxiangshu.Tests.OmpCapsTests
open Wanxiangshu.Tests.OmpFuzzyTests
open Wanxiangshu.Tests.OmpPluginTests
open Wanxiangshu.Tests.OmpPluginTestsLifecycle
open Wanxiangshu.Tests.OmpPluginTestsAgentEnd
open Wanxiangshu.Tests.OmpReviewTests
open Wanxiangshu.Tests.OmpReviewLoopAsyncTests
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
open Wanxiangshu.Tests.OmpToolResultEventTests
open Wanxiangshu.Tests.OmpPluginCoreIntegrationTests
open Wanxiangshu.Tests.OmpExecutorToolsTests
open Wanxiangshu.Tests.SubagentIoTests
open Wanxiangshu.Tests.OmpCoverage2Tests
open Wanxiangshu.Tests.OmpTodoToolTests

let ompTestEntries () : (string * TestBody) list =
    [ "OmpKernelTests.filterOmpMainSessionTools", Sync(sync OmpKernelTests.filterOmpMainSessionTools)
      "OmpKernelTests.validateFetchUrlBlocksPrivate", Sync(sync OmpKernelTests.validateFetchUrlBlocksPrivate)
      "OmpKernelTests.reviewInstructionsCanonicalVerdictTool",
      Sync(sync OmpKernelTests.reviewInstructionsCanonicalVerdictTool)
      "OmpSessionToolsTests.mainSessionStripsChildOnlyAndBash",
      Sync(sync OmpSessionToolsTests.mainSessionStripsChildOnlyAndBash)
      "OmpSessionToolsTests.childSessionKeepsChildTools", Sync(sync OmpSessionToolsTests.childSessionKeepsChildTools)
      "OmpWebFetchTests.blocksLocalhostAndPrivateRanges", Sync(sync OmpWebFetchTests.blocksLocalhostAndPrivateRanges)
      "OmpWebFetchTests.rejectsUnsupportedScheme", Sync(sync OmpWebFetchTests.rejectsUnsupportedScheme)
      "OmpCapsTests.buildCapsFromUppercaseFiles", Async OmpCapsTests.buildCapsFromUppercaseFiles
      "OmpCapsTests.stripHostDirContext", Sync(sync OmpCapsTests.stripHostDirContext)
      "OmpCapsTests.appendCapsIdempotent", Async OmpCapsTests.appendCapsIdempotent
      "OmpCapsTests.capsSkipsExcludedDirs", Async OmpCapsTests.capsSkipsExcludedDirs
      "OmpCapsTests.capsRespectsFileCountBudget", Async OmpCapsTests.capsRespectsFileCountBudget
      "OmpFuzzyTests.fuzzyFindIteratorSingleUse", Sync(sync OmpFuzzyTests.fuzzyFindIteratorSingleUse)
      "OmpFuzzyTests.fuzzyGrepIteratorSingleUse", Sync(sync OmpFuzzyTests.fuzzyGrepIteratorSingleUse)
      "OmpFuzzyTests.registeredFuzzyToolsExposeIteratorParam",
      Async OmpFuzzyTests.registeredFuzzyToolsExposeIteratorParam
      "OmpKernelTests.executorSummarizerPromptCarriesWhatToSummarize",
      Sync(sync OmpKernelTests.executorSummarizerPromptCarriesWhatToSummarize)
      "OmpPluginTests.registersCoreToolsIdempotent", Async OmpPluginTests.registersCoreToolsIdempotent
      "OmpPluginTests.meditatorSchemaUnifiedNote", Async OmpPluginTests.meditatorSchemaUnifiedNote
      "OmpPluginTests.sessionStartStripsMainSessionTools", Async OmpPluginTests.sessionStartStripsMainSessionTools
      "OmpPluginTests.fuzzyDescriptionsMatchMuxWording", Sync(sync OmpPluginTests.fuzzyDescriptionsMatchMuxWording)
      "OmpContextTransformTests.capsSynthUserPrepended", Async OmpContextTransformTests.capsSynthUserPrepended
      "OmpContextTransformTests.capsReadToolsInContextTransform",
      Async OmpContextTransformTests.capsReadToolsInContextTransform
      "OmpContextTransformTests.testInvestigatorCrashWithUndefinedCaps",
      Async OmpContextTransformTests.testInvestigatorCrashWithUndefinedCaps
      "OmpContextTransformTests.beforeAgentStartOmitsCapsXml",
      Async OmpContextTransformTests.beforeAgentStartOmitsCapsXml
      "OmpContextTransformTests.reviewReplayIfStoreEmptyOnTransform",
      Async OmpContextTransformTests.reviewReplayIfStoreEmptyOnTransform
      "OmpPluginTests.readAssistantTextFromEntries", Sync(sync OmpPluginTests.readAssistantTextFromEntries)
      "OmpPluginTests.subagentPromptsContainKernelFragments",
      Sync(sync OmpPluginTests.subagentPromptsContainKernelFragments)
      "OmpPluginTests.executorToolSchemaFourFields", Async OmpPluginTests.executorToolSchemaFourFields
      "OmpPluginTests.browserErrorsWithoutBrowserHost", Async OmpPluginTests.browserErrorsWithoutBrowserHost
      "OmpPluginTests.reviewChildInitialPromptUsesReturnReviewer",
      Sync(sync OmpPluginTests.reviewChildInitialPromptUsesReturnReviewer)
      "OmpPluginTests.fuzzyGrepExcludeAnyOfLength2", Async OmpPluginTests.fuzzyGrepExcludeAnyOfLength2
      "OmpPluginTestsLifecycle.websearchSchemaRequiresQueryAndWhatToSummarize",
      Async OmpPluginTestsLifecycle.websearchSchemaRequiresQueryAndWhatToSummarize
      "OmpPluginTestsAgentEnd.run", Async OmpPluginTestsAgentEnd.run
      "OmpReviewTests.returnReviewerVerdictPerfectRevise", Async OmpReviewTests.returnReviewerVerdictPerfectRevise
      "OmpReviewTests.returnReviewerViaSetPendingStateForTest",
      Async OmpReviewTests.returnReviewerViaSetPendingStateForTest
      "OmpReviewTests.runReviewLoopChildToolNames", Async OmpReviewTests.runReviewLoopChildToolNames
      "OmpReviewTests.runReviewLoopAcceptsWhenPendingResolved",
      Async OmpReviewTests.runReviewLoopAcceptsWhenPendingResolved
      "OmpReviewLoopAsyncTests.runReviewLoopResolvesViaAsyncCallbackNotPolling",
      Async OmpReviewLoopAsyncTests.runReviewLoopResolvesViaAsyncCallbackNotPolling
      "OmpReviewLoopAsyncTests.runReviewLoopSendsNudgeOnTimeoutThenStopsOnResolve",
      Async OmpReviewLoopAsyncTests.runReviewLoopSendsNudgeOnTimeoutThenStopsOnResolve
      "OmpChildSessionTests.createChildSessionReviewToolNames",
      Async OmpChildSessionTests.createChildSessionReviewToolNames
      "OmpChildSessionTests.createChildSessionRunnerToolNames",
      Async OmpChildSessionTests.createChildSessionRunnerToolNames
      "OmpRunnerTests.waitRunnerJobAfterAppendLog", Async OmpRunnerTests.waitRunnerJobAfterAppendLog
      "OmpRunnerTests.setRunnerJobStateForTestHasRunning", Sync(sync OmpRunnerTests.setRunnerJobStateForTestHasRunning)
      "OmpRunnerTests.abortRunnerJobClearsRunning", Sync(sync OmpRunnerTests.abortRunnerJobClearsRunning)
      "OmpRunnerTests.cleanupRunnerJobClearsRunning", Async OmpRunnerTests.cleanupRunnerJobClearsRunning
      "OmpRunnerTests.hasRunningWhenActiveExecutorRun", Sync(sync OmpRunnerTests.hasRunningWhenActiveExecutorRun)
      "OmpRunnerTests.abortExecutorRunClearsActive", Sync(sync OmpRunnerTests.abortExecutorRunClearsActive)
      "OmpRunnerTests.executorChildToolNamesMatchOmpSessionTools",
      Sync(sync OmpRunnerTests.executorChildToolNamesMatchOmpSessionTools)
      "OmpHelpersTests.checkSyntaxBadJson", Async OmpHelpersTests.checkSyntaxBadJson
      "OmpHelpersTests.checkSyntaxValidJson", Async OmpHelpersTests.checkSyntaxValidJson
      "OmpHelpersTests.checkSyntaxBrokenJsonReports_intentionalWarningFork",
      Async OmpHelpersTests.checkSyntaxBrokenJsonReports_intentionalWarningFork
      "OmpHelpersTests.checkSyntaxMarkdownExempt", Async OmpHelpersTests.checkSyntaxMarkdownExempt
      "OmpHelpersTests.supportsSyntaxDiagnosticsGrepFalse",
      Sync(sync OmpHelpersTests.supportsSyntaxDiagnosticsGrepFalse)
      "OmpHelpersTests.stripHeadTailViaKernel", Sync(sync OmpHelpersTests.stripHeadTailViaKernel)
      "OmpTitleFetchGuardTests.signature", Sync(sync OmpTitleFetchGuardTests.signature)
      "OmpTitleFetchGuardTests.wrapText", Sync(sync OmpTitleFetchGuardTests.wrapText)
      "OmpTitleFetchGuardTests.detectProbeUserContent", Sync(sync OmpTitleFetchGuardTests.detectProbeUserContent)
      "OmpTitleFetchGuardTests.rejectNonProbeBody", Sync(sync OmpTitleFetchGuardTests.rejectNonProbeBody)
      "OmpTitleFetchGuardTests.rejectNonJsonBody", Sync(sync OmpTitleFetchGuardTests.rejectNonJsonBody)
      "OmpTitleFetchGuardTests.rewriteStringContent", Sync(sync OmpTitleFetchGuardTests.rewriteStringContent)
      "OmpTitleFetchGuardTests.rewriteArrayContent", Sync(sync OmpTitleFetchGuardTests.rewriteArrayContent)
      "SubagentIoTests.firstStringFindsFirst", Sync(sync SubagentIoTests.firstStringFindsFirst)
      "SubagentIoTests.extractToolContextFallsBackToPluginDirectory",
      Sync(sync SubagentIoTests.extractToolContextFallsBackToPluginDirectory)
      "SubagentIoTests.extractToolContextUsesDirectory", Sync(sync SubagentIoTests.extractToolContextUsesDirectory)
      "SubagentIoTests.textPartsReturnsArrayOfTextParts", Sync(sync SubagentIoTests.textPartsReturnsArrayOfTextParts)
      "SubagentIoTests.buildPromptBodyBasic", Sync(sync SubagentIoTests.buildPromptBodyBasic)
      "SubagentIoTests.signalAbortedNullIsFalse", Sync(sync SubagentIoTests.signalAbortedNullIsFalse)
      "OmpPluginCoreTests.reviewStoreIsSharedSingleton", Sync(sync OmpPluginCoreTests.reviewStoreIsSharedSingleton)
      "OmpPluginCoreTests.clearReviewStatesNoError", Sync(sync OmpPluginCoreTests.clearReviewStatesNoError)
      "OmpPluginCoreTests.abortHookDeactivatesReview", Async OmpPluginCoreTests.abortHookDeactivatesReview
      "OmpPluginCoreTests.streamAbortHookDeactivatesReview", Async OmpPluginCoreTests.streamAbortHookDeactivatesReview
      "OmpPluginCoreTests.sessionErrorHookDeactivatesReview", Async OmpPluginCoreTests.sessionErrorHookDeactivatesReview
      "OmpPluginCoreTests.unrelatedEventLeavesReviewActive", Async OmpPluginCoreTests.unrelatedEventLeavesReviewActive
      "OmpMagicTodoTests.sharedSessionStoreByHost", Sync(sync OmpMagicTodoTests.sharedSessionStoreByHost)
      "OmpMagicTodoTests.hostPartitionedReports", Sync(sync OmpMagicTodoTests.hostPartitionedReports)
      "OmpMagicTodoTests.backlogEntryFromTodoInputHostAgnostic",
      Sync(sync OmpMagicTodoTests.backlogEntryFromTodoInputHostAgnostic)
      "OmpMagicTodoTests.inputOfPartNonTool", Sync(sync OmpMagicTodoTests.inputOfPartNonTool)
      "OmpMagicTodoTests.replayBacklogOmpFallsBackToCapturedReport",
      Sync(sync OmpMagicTodoTests.replayBacklogOmpFallsBackToCapturedReport)
      "OmpToolResultEventTests.getToolInputPrefersInputOverArgs",
      Sync(sync OmpToolResultEventTests.getToolInputPrefersInputOverArgs)
      "OmpToolResultEventTests.getToolInputFallsBackToArgs",
      Sync(sync OmpToolResultEventTests.getToolInputFallsBackToArgs)
      "OmpToolResultEventTests.getToolInputReturnsNullishWhenNeitherPresent",
      Sync(sync OmpToolResultEventTests.getToolInputReturnsNullishWhenNeitherPresent)
      "OmpToolResultEventTests.getToolCallIdPrefersToolCallId",
      Sync(sync OmpToolResultEventTests.getToolCallIdPrefersToolCallId)
      "OmpToolResultEventTests.getToolCallIdFallsBackToCallId",
      Sync(sync OmpToolResultEventTests.getToolCallIdFallsBackToCallId)
      "OmpToolResultEventTests.getToolCallIdFallsBackToCallID",
      Sync(sync OmpToolResultEventTests.getToolCallIdFallsBackToCallID)
      "OmpToolResultEventTests.getToolCallIdReturnsEmptyWhenNonePresent",
      Sync(sync OmpToolResultEventTests.getToolCallIdReturnsEmptyWhenNonePresent)
      "OmpToolResultEventTests.getToolResultTextFromContentArray",
      Sync(sync OmpToolResultEventTests.getToolResultTextFromContentArray)
      "OmpToolResultEventTests.getToolResultTextFromContentArrayMixed",
      Sync(sync OmpToolResultEventTests.getToolResultTextFromContentArrayMixed)
      "OmpToolResultEventTests.getToolResultTextFromStringContent",
      Sync(sync OmpToolResultEventTests.getToolResultTextFromStringContent)
      "OmpToolResultEventTests.setToolResultTextLeavesReadableText",
      Sync(sync OmpToolResultEventTests.setToolResultTextLeavesReadableText)
      "OmpToolResultEventTests.setToolResultTextPreservesArrayForm",
      Sync(sync OmpToolResultEventTests.setToolResultTextPreservesArrayForm)
      "OmpToolResultEventTests.setToolResultTextContentIsStringAfterWrite",
      Sync(sync OmpToolResultEventTests.setToolResultTextContentIsStringAfterWrite)
      "OmpPluginCoreIntegrationTests.extensionIsIdempotent", Async OmpPluginCoreIntegrationTests.extensionIsIdempotent
      "OmpPluginCoreIntegrationTests.extensionRegistersLifecycleHooks",
      Async OmpPluginCoreIntegrationTests.extensionRegistersLifecycleHooks
      "OmpPluginCoreIntegrationTests.reviewStoreSharedWithTools",
      Async OmpPluginCoreIntegrationTests.reviewStoreSharedWithTools
      "OmpAgentConfigTests.applyAgentConfigForRegistersBuiltinAgents",
      Sync(sync OmpAgentConfigTests.applyAgentConfigForRegistersBuiltinAgents)
      "OmpAgentConfigTests.applyAgentConfigForPreservesUserOverrides",
      Sync(sync OmpAgentConfigTests.applyAgentConfigForPreservesUserOverrides)
      "OmpAgentConfigTests.disableNativeAgentsClearsMemoryAndCheckpoint",
      Sync(sync OmpAgentConfigTests.disableNativeAgentsClearsMemoryAndCheckpoint)
      "OmpAgentConfigTests.disableNativeAgentsPreservesUserOverrides",
      Sync(sync OmpAgentConfigTests.disableNativeAgentsPreservesUserOverrides)
      "OmpAgentConfigTests.applyAgentConfigForPreservesUserPermissionAndMcps",
      Sync(sync OmpAgentConfigTests.applyAgentConfigForPreservesUserPermissionAndMcps)
      "OmpAgentConfigTests.applyAgentConfigForKeepsUserCustomAgents",
      Sync(sync OmpAgentConfigTests.applyAgentConfigForKeepsUserCustomAgents)
      "OmpAgentConfigTests.disableNativeAgentsReplacesCheckpointSection",
      Sync(sync OmpAgentConfigTests.disableNativeAgentsReplacesCheckpointSection)
      "OmpHookExecuteTests.hookCoderInjectUiLabel", Sync(sync OmpHookExecuteTests.hookCoderInjectUiLabel)
      "OmpHookExecuteTests.hookInvestigatorInjectUiLabel", Sync(sync OmpHookExecuteTests.hookInvestigatorInjectUiLabel)
      "OmpHookExecuteTests.hookNonSubagentDoesNotInjectUiLabel",
      Sync(sync OmpHookExecuteTests.hookNonSubagentDoesNotInjectUiLabel)
      "OmpHookExecuteTests.hookApplyPatchNormalisesPatchToPatchText",
      Sync(sync OmpHookExecuteTests.hookApplyPatchNormalisesPatchToPatchText)
      "OmpHookExecuteTests.hookApplyPatchStringArgsIsNoOp",
      Sync(sync OmpHookExecuteTests.hookApplyPatchStringArgsIsNoOp)
      "OmpHookExecuteTests.hookPatchNameNormalisesToPatchText",
      Sync(sync OmpHookExecuteTests.hookPatchNameNormalisesToPatchText)
      "OmpHookExecuteTests.hookApplyPatchLeavesExistingPatchTextUntouched",
      Sync(sync OmpHookExecuteTests.hookApplyPatchLeavesExistingPatchTextUntouched)
      "OmpSessionLifecycleTests.isChildSessionGuard", Sync(sync OmpSessionLifecycleTests.isChildSessionGuard)
      "OmpPluginTestsLifecycle.extensionRegistersLifecycleHooks",
      Async OmpPluginTestsLifecycle.extensionRegistersLifecycleHooks
      "OmpPluginTestsLifecycle.toolCallHookCanBeInvoked", Async OmpPluginTestsLifecycle.toolCallHookCanBeInvoked
      "OmpPluginTestsLifecycle.toolCallBlocksChildOnlyInMainSession",
      Async OmpPluginTestsLifecycle.toolCallBlocksChildOnlyInMainSession
      "OmpPluginTestsLifecycle.turnStartRestoresMainSessionTools",
      Async OmpPluginTestsLifecycle.turnStartRestoresMainSessionTools
      "OmpCoverage2Tests.run", Async OmpCoverage2Tests.run
      "OmpTodoToolTests.run", Async OmpTodoToolTests.run
      "OmpExecutorToolsTests.registersExecutorTools", Sync(sync OmpExecutorToolsTests.registersExecutorTools)
      "OmpExecutorToolsTests.run", Async OmpExecutorToolsTests.run ]
