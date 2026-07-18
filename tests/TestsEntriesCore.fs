module Wanxiangshu.Tests.TestsEntriesCore

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestsTestBody
open Wanxiangshu.Tests.TestsEntriesFallback
open Wanxiangshu.Tests.ReviewTests
open Wanxiangshu.Tests.ReviewTestsReplay
open Wanxiangshu.Tests.ReviewTestsPrompts
open Wanxiangshu.Tests.ReviewSessionEffectsTests
open Wanxiangshu.Tests.AgentTests
open Wanxiangshu.Tests.AgentNudgeSpecs
open Wanxiangshu.Tests.AgentNudgeSpecsWip
open Wanxiangshu.Tests.AgentNudgeSpecsDecode
open Wanxiangshu.Tests.PtyReadThrottleTests
open Wanxiangshu.Tests.KernelTests
open Wanxiangshu.Tests.DynFieldTests
open Wanxiangshu.Tests.KernelPromptSpecs
open Wanxiangshu.Tests.SubagentDispatcherTests
open Wanxiangshu.Tests.KernelPolicyTests
open Wanxiangshu.Tests.ReviewSessionStateMachineTests
open Wanxiangshu.Tests.HostToolsTests
open Wanxiangshu.Tests.ToolPermissionTests
open Wanxiangshu.Tests.SubagentPromptsTests
open Wanxiangshu.Tests.SubagentIntentsTests
open Wanxiangshu.Tests.SubagentIteratorStoreTests
open Wanxiangshu.Tests.NudgeRetryProgressTests
open Wanxiangshu.Tests.NudgeTodoStatusTests
open Wanxiangshu.Tests.NudgeEventSourcingTests
open Wanxiangshu.Tests.OpencodeNudgeLifecycleTests
open Wanxiangshu.Tests.ReviewSessionRegistryTests
open Wanxiangshu.Tests.ReviewSessionQueryTests
open Wanxiangshu.Tests.ReviewPromptsFormatTests
open Wanxiangshu.Tests.EventLogFoldTests
open Wanxiangshu.Tests.EventLogReviewLoopFoldTests
open Wanxiangshu.Tests.WanxiangzhenSquadEventTests
open Wanxiangshu.Tests.EventLogCodecTests
open Wanxiangshu.Tests.EventLogRuntimeTests
open Wanxiangshu.Tests.EventLogRuntimeRobustnessTests
open Wanxiangshu.Tests.KernelAndSupportCoverageEntries
open Wanxiangshu.Tests.TestsEntriesSessionLoop

let private reviewBlock () : (string * TestBody) list =
    [ "ReviewTests.transition'", TestBody.Sync(sync ReviewTests.transition')
      "ReviewTests.registry", TestBody.Sync(sync ReviewTests.registry)
      "ReviewTests.resultMapping", TestBody.Sync(sync ReviewTests.resultMapping)
      "ReviewTests.reviewerLoop", TestBody.Sync(sync ReviewTests.reviewerLoop)
      "ReviewTests.runtime", TestBody.Sync(sync ReviewTests.runtime)
      "ReviewReportBufferTests.run", TestBody.Sync(sync ReviewReportBufferTests.run)
      "PtyReadThrottleTests.run", TestBody.Async PtyReadThrottleTests.run
      "ReviewTests.deactivateParentPreservesChildPending",
      TestBody.Sync(sync ReviewTests.deactivateParentPreservesChildPending)
      "ReviewTests.promptPartsBranches", TestBody.Sync(sync ReviewTests.promptPartsBranches)
      "ReviewTests.resolvePendingClearsSuppressor", TestBody.Sync(sync ReviewTests.resolvePendingClearsSuppressor) ]

let private reviewSessionBlock () : (string * TestBody) list =
    [ "ReviewSessionEffectsTests.emptyEffectsHasEmptyMaps",
      TestBody.Sync(sync ReviewSessionEffectsTests.emptyEffectsHasEmptyMaps)
      "ReviewSessionEffectsTests.setPendingAddsEntry", TestBody.Sync(sync ReviewSessionEffectsTests.setPendingAddsEntry)
      "ReviewSessionEffectsTests.resolvePendingFiresCallback",
      TestBody.Sync(sync ReviewSessionEffectsTests.resolvePendingFiresCallback)
      "ReviewSessionEffectsTests.resolvePendingUnknownIdReturnsFalse",
      TestBody.Sync(sync ReviewSessionEffectsTests.resolvePendingUnknownIdReturnsFalse)
      "ReviewSessionEffectsTests.disposeSessionTreeSkipsUnknownIds",
      TestBody.Sync(sync ReviewSessionEffectsTests.disposeSessionTreeSkipsUnknownIds)
      "ReviewSessionEffectsTests.disposeSessionTreeTerminatesAll",
      TestBody.Sync(sync ReviewSessionEffectsTests.disposeSessionTreeTerminatesAll)
      "ReviewSessionRegistryTests.run", TestBody.Sync(sync ReviewSessionRegistryTests.run)
      "ReviewSessionQueryTests.run", TestBody.Sync(sync ReviewSessionQueryTests.run)
      "ReviewPromptsFormatTests.run", TestBody.Sync(sync ReviewPromptsFormatTests.run)
      "ReviewSessionStateMachineTests.run", TestBody.Sync(sync ReviewSessionStateMachineTests.run) ]

let private replayBlock () : (string * TestBody) list =
    [ "ReviewTests.disposeSessionTreeTerminatesAll",
      TestBody.Sync(sync ReviewTestsReplay.disposeSessionTreeTerminatesAll)
      "EventLogFoldTests.run", TestBody.Sync(sync EventLogFoldTests.run)
      "EventLogReviewLoopFoldTests.run", TestBody.Sync(sync EventLogReviewLoopFoldTests.run)
      "WanxiangzhenSquadEventTests.run", TestBody.Sync(sync WanxiangzhenSquadEventTests.run)
      "EventLogCodecTests.run", TestBody.Sync(sync EventLogCodecTests.run)
      "EventLogRuntimeTests.run", TestBody.Async EventLogRuntimeTests.run
      "EventLogRuntimeRobustnessTests.run", TestBody.Async EventLogRuntimeRobustnessTests.run
      "ReviewTests.inferReviewTaskFromTexts'", TestBody.Sync(sync ReviewTestsReplay.inferReviewTaskFromTexts')
      "ReviewTests.parseFrontMatterScalars'", TestBody.Sync(sync ReviewTestsReplay.parseFrontMatterScalars') ]

let private reviewPromptsBlock () : (string * TestBody) list =
    [ "KernelPromptSpecsReview.yamlFrontMatterRoundTrip",
      TestBody.Sync(sync KernelPromptSpecsReview.yamlFrontMatterRoundTrip)
      "ReviewTests.doubleCheckAnchorReplay", TestBody.Sync(sync ReviewTestsPrompts.doubleCheckAnchorReplay)
      "ReviewTests.doubleCheckPromptFormat", TestBody.Sync(sync ReviewTestsPrompts.doubleCheckPromptFormat)
      "ReviewTests.reviewerPromptFormat", TestBody.Sync(sync ReviewTestsPrompts.reviewerPromptFormat)
      "ReviewTests.muxReviewerVerdictPromptFormat",
      TestBody.Sync(sync ReviewTestsPrompts.muxReviewerVerdictPromptFormat)
      "ReviewTests.reviewInstructionsFrontMatter", TestBody.Sync(sync ReviewTestsPrompts.reviewInstructionsFrontMatter) ]

let private agentAndNudgeBlock () : (string * TestBody) list =
    [ "AgentTests.canUse'", TestBody.Sync(sync AgentTests.canUse')
      "AgentTests.canUseMatrix", TestBody.Sync(sync AgentTests.canUseMatrix)
      "AgentTests.deniedTools'", TestBody.Sync(sync AgentTests.deniedTools')
      "AgentNudgeSpecs.decision", TestBody.Sync(sync AgentNudgeSpecs.decision)
      "AgentNudgeSpecs.test_isNaturalStop", TestBody.Sync(sync AgentNudgeSpecs.test_isNaturalStop)
      "AgentNudgeSpecs.dedupFromIntegral", TestBody.Sync(sync AgentNudgeSpecs.dedupFromIntegral)
      "AgentNudgeSpecs.decideNudge'", TestBody.Sync(sync AgentNudgeSpecs.decideNudge')
      "AgentNudgeSpecs.selectPrompt", TestBody.Sync(sync AgentNudgeSpecs.selectPrompt)
      "AgentNudgeSpecs.runAsync", TestBody.Async AgentNudgeSpecs.runAsync
      "AgentNudgeSpecs.submitReviewWipNudgeDedup", TestBody.Sync(sync AgentNudgeSpecsWip.submitReviewWipNudgeDedup)
      "AgentNudgeSpecs.decodeTodosOpenItems", TestBody.Sync(sync AgentNudgeSpecsDecode.decodeTodosOpenItems)
      "AgentNudgeSpecsWip.submitReviewWipNudgeDedup", TestBody.Sync(sync AgentNudgeSpecsWip.submitReviewWipNudgeDedup)
      "AgentNudgeSpecsDecode.decodeTodosOpenItems", TestBody.Sync(sync AgentNudgeSpecsDecode.decodeTodosOpenItems) ]

let private nudgeLifecycleBlock () : (string * TestBody) list =
    [ "NudgeRetryProgressTests.run", TestBody.Sync(sync NudgeRetryProgressTests.run)
      "NudgeTodoStatusTests.run", TestBody.Sync(sync NudgeTodoStatusTests.run)
      "NudgeEventSourcingTests.run", TestBody.Sync(sync NudgeEventSourcingTests.run)
      "OpencodeNudgeLifecycleTests.run", TestBody.Sync(sync OpencodeNudgeLifecycleTests.run)
      "OpencodeNudgeLifecycleTests.runAsync", TestBody.Async OpencodeNudgeLifecycleTests.runAsync
      "KernelPolicyTests.run", TestBody.Sync(sync KernelPolicyTests.run)
      "HostToolsTests.run", TestBody.Sync(sync HostToolsTests.run)
      "ToolPermissionTests.run", TestBody.Sync(sync ToolPermissionTests.run)
      "SubagentPromptsTests.run", TestBody.Sync(sync SubagentPromptsTests.run)
      "SubagentIntentsTests.run", TestBody.Sync(sync SubagentIntentsTests.run)
      "SubagentIteratorStoreTests.run", TestBody.Sync(sync SubagentIteratorStoreTests.run)
      "SubagentDispatcherTests.run", TestBody.Async SubagentDispatcherTests.run ]

let private kernelBlock () : (string * TestBody) list =
    [ "KernelTests.headTail'", TestBody.Sync(sync KernelTests.headTail')
      "KernelTests.stripLexer'", TestBody.Sync(sync KernelTests.stripLexer')
      "KernelTests.jsBoundary'", TestBody.Sync(sync KernelTests.jsBoundary')
      "KernelTests.finishReason'", TestBody.Sync(sync KernelTests.finishReason')
      "KernelPromptSpecs.hostKernel'", TestBody.Sync(sync KernelPromptSpecs.hostKernel')
      "DynFieldTests.run", TestBody.Sync(sync DynFieldTests.run)
      "KernelPromptSpecs.toolCatalogCentralized", TestBody.Sync(sync KernelPromptSpecs.toolCatalogCentralized)
      "KernelPromptSpecs.subagentDispatch", TestBody.Sync(sync KernelPromptSpecs.subagentDispatch)
      "KernelPromptSpecs.mimocodeFormatPromptAppendsAgentReportTail",
      TestBody.Sync(sync KernelPromptSpecs.mimocodeFormatPromptAppendsAgentReportTail)
      "KernelPromptSpecs.subagentJoinReports", TestBody.Sync(sync KernelPromptSpecs.subagentJoinReports)
      "KernelPromptSpecs.meditatorMentionsReadCapability",
      TestBody.Sync(sync KernelPromptSpecs.meditatorMentionsReadCapability)
      "KernelTests.dynDeleteKey", TestBody.Sync(sync KernelTests.dynDeleteKey)
      "KernelPromptSpecs.loopMessagesShared", TestBody.Sync(sync KernelPromptSpecs.loopMessagesShared)
      "KernelPromptSpecs.reviewerVerdictPromptsShared",
      TestBody.Sync(sync KernelPromptSpecs.reviewerVerdictPromptsShared)
      "KernelPromptSpecs.reviewResultFormattingShared",
      TestBody.Sync(sync KernelPromptSpecs.reviewResultFormattingShared)
      "KernelPromptSpecs.reviewVerdictDecode", TestBody.Sync(sync KernelPromptSpecs.reviewVerdictDecode)
      "KernelPromptSpecs.reviewDecisionPolicy", TestBody.Sync(sync KernelPromptSpecs.reviewDecisionPolicy)
      "KernelPromptSpecs.reviewMarkdownCodec", TestBody.Sync(sync KernelPromptSpecs.reviewMarkdownCodec)
      "KernelPromptSpecs.executorSummarizerNoExitStatus",
      TestBody.Sync(sync KernelPromptSpecs.executorSummarizerNoExitStatus)
      "KernelPromptSpecs.domainErrorsShared", TestBody.Sync(sync KernelPromptSpecs.domainErrorsShared) ]

let coreTestEntries () : (string * TestBody) list =
    reviewBlock ()
    @ reviewSessionBlock ()
    @ replayBlock ()
    @ reviewPromptsBlock ()
    @ agentAndNudgeBlock ()
    @ nudgeLifecycleBlock ()
    @ kernelBlock ()
    @ sessionLoopTestEntries ()
    @ TestsEntriesCoreTail.tailCoreTestEntries ()
    @ TestsEntriesFallback.tailTestEntries ()
    @ fallbackTestEntries ()
    @ TestsEntriesFuzzy.fuzzyTestEntries ()
    @ TestsEntriesDomain.domainTestEntries ()
    @ KernelAndSupportCoverageEntries.kernelSupportCoverageTestEntries ()
