module Wanxiangshu.Tests.TestsEntriesCore
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.EventDrivenHarnessDemo
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
open Wanxiangshu.Tests.KernelTests
open Wanxiangshu.Tests.DynFieldTests
open Wanxiangshu.Tests.KernelPromptSpecs
open Wanxiangshu.Tests.FuzzyTests
open Wanxiangshu.Tests.FuzzyTestsPaging
open Wanxiangshu.Tests.ShellTests
open Wanxiangshu.Tests.ShellTestsFormat
open Wanxiangshu.Tests.DynTests
open Wanxiangshu.Tests.DelegateTests
open Wanxiangshu.Tests.DelegateToolsCodecTests
open Wanxiangshu.Tests.ResolveAiSettingsTests
open Wanxiangshu.Tests.IntegrationPluginTests
open Wanxiangshu.Tests.IntegrationEventTests
open Wanxiangshu.Tests.IntegrationDedupTests
open Wanxiangshu.Tests.IntegrationToolSpecCatalog
open Wanxiangshu.Tests.IntegrationOpencodeReviewSpecs
open Wanxiangshu.Tests.IntegrationChatTests
open Wanxiangshu.Tests.IntegrationChatTestsSubagent
open Wanxiangshu.Tests.OpencodeSessionLifecycleTests
open Wanxiangshu.Tests.WorkBacklogTests
open Wanxiangshu.Tests.MethodologyTests

open Wanxiangshu.Tests.LoopMessagesTests
open Wanxiangshu.Tests.TitleFetchGuardTests
open Wanxiangshu.Tests.ReviewReplaySyncTests
open Wanxiangshu.Tests.EventLogFoldTests
open Wanxiangshu.Tests.EventLogCodecTests
open Wanxiangshu.Tests.EventLogRuntimeTests
open Wanxiangshu.Tests.CapsSynthCommonTests
open Wanxiangshu.Tests.CapsFileCacheTests
open Wanxiangshu.Tests.DedupTests
open Wanxiangshu.Tests.MessagingTests
open Wanxiangshu.Tests.SubagentPromptBuildTests
open Wanxiangshu.Tests.SubagentSpawnTests
open Wanxiangshu.Tests.SubagentDispatcherTests
open Wanxiangshu.Tests.WebToolsCodecTests
open Wanxiangshu.Tests.ReviewToolsCodecTests
open Wanxiangshu.Tests.ExecutorToolsCodecTests
open Wanxiangshu.Tests.ToolExecuteTests
open Wanxiangshu.Tests.ToolArgsDecodeTests
open Wanxiangshu.Tests.ToolResultWireTests
open Wanxiangshu.Tests.KernelHelpersTests

open Wanxiangshu.Tests.ReviewPromptsFormatTests
open Wanxiangshu.Tests.SubagentToolExecuteTests
open Wanxiangshu.Tests.FileToolsCodecTests
open Wanxiangshu.Tests.FuzzyToolsCodecTests
open Wanxiangshu.Tests.ReviewSessionRegistryTests
open Wanxiangshu.Tests.WorkBacklogToolsCodecTests
open Wanxiangshu.Tests.PatchToolsCodecTests
open Wanxiangshu.Tests.PatchParserTests
open Wanxiangshu.Tests.HostMessagePartCodecTests
open Wanxiangshu.Tests.MessagingPartCodecTests
open Wanxiangshu.Tests.ToolContextCodecTests
open Wanxiangshu.Tests.OpencodeContextCodecTests
open Wanxiangshu.Tests.OpencodeSessionPromptCodecTests
open Wanxiangshu.Tests.OpencodeSessionSpawnCodecTests
open Wanxiangshu.Tests.SessionIoPromptBodyTests
open Wanxiangshu.Tests.OpencodeAgentConfigCodecTests
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
open Wanxiangshu.Tests.OmpChildSessionTests
open Wanxiangshu.Tests.OmpAgentConfigTests
open Wanxiangshu.Tests.OmpHookExecuteTests
open Wanxiangshu.Tests.OmpSessionLifecycleTests
open Wanxiangshu.Tests.OmpPluginCoreTests
open Wanxiangshu.Tests.OmpTitleFetchGuardTests
open Wanxiangshu.Tests.OmpMagicTodoTests
open Wanxiangshu.Tests.OmpPluginCoreIntegrationTests
open Wanxiangshu.Tests.SubagentIoTests
open Wanxiangshu.Tests.MessageTransformPolicyTests
open Wanxiangshu.Tests.SembleInjectionTests
open Wanxiangshu.Tests.SembleReviewerInjectionTests
open Wanxiangshu.Tests.ToolCatalogClassificationTests
open Wanxiangshu.Tests.ToolOutputInfoTests
open Wanxiangshu.Tests.ExecutorKernelTests
open Wanxiangshu.Tests.NudgeRetryProgressTests
open Wanxiangshu.Tests.NudgeTodoStatusTests
open Wanxiangshu.Tests.NudgeEventSourcingTests
open Wanxiangshu.Tests.ReviewSessionStateMachineTests
open Wanxiangshu.Tests.HostToolsTests
open Wanxiangshu.Tests.ToolPermissionTests
open Wanxiangshu.Tests.SubagentPromptsTests
open Wanxiangshu.Tests.SubagentIntentsTests
open Wanxiangshu.Tests.MethodologyRegistryTests
open Wanxiangshu.Tests.ToolCatalogRegistryTests
open Wanxiangshu.Tests.TreeSitterKernelTests
open Wanxiangshu.Tests.ConfigTests
open Wanxiangshu.Tests.JsonSchemaBuildersTests
open Wanxiangshu.Tests.ExecutorStripTests
open Wanxiangshu.Tests.WebFetchGuardTests
open Wanxiangshu.Tests.ReviewVerdictTests
open Wanxiangshu.Tests.ToolCopyTests
open Wanxiangshu.Tests.JsArrayMutateTests
open Wanxiangshu.Tests.TestsEntriesDomain
open Wanxiangshu.Tests.TestsEntriesCoverage
open Wanxiangshu.Tests.TestsEntriesFuzzy
open Wanxiangshu.Tests.TestsTestBody
open Wanxiangshu.Tests.WarnTddKernelFactsTests
open Wanxiangshu.Tests.WarnTddOpencodeEnforcementTests
open Wanxiangshu.Tests.WarnTddMuxEnforcementTests
open Wanxiangshu.Tests.WarnTddOmpEnforcementTests
let coreTestEntries () : (string * TestBody) list =
    [
    "ReviewTests.transition'", TestBody.Sync (sync ReviewTests.transition')
    "ReviewTests.registry", TestBody.Sync (sync ReviewTests.registry)
    "ReviewTests.resultMapping", TestBody.Sync (sync ReviewTests.resultMapping)
    "ReviewTests.reviewerLoop", TestBody.Sync (sync ReviewTests.reviewerLoop)
    "ReviewTests.runtime", TestBody.Sync (sync ReviewTests.runtime)
    "ReviewTests.promptPartsBranches", TestBody.Sync (sync ReviewTests.promptPartsBranches)
    "ReviewTests.resolvePendingClearsSuppressor", TestBody.Sync (sync ReviewTests.resolvePendingClearsSuppressor)
    "ReviewSessionEffectsTests.emptyEffectsHasEmptyMaps", TestBody.Sync (sync ReviewSessionEffectsTests.emptyEffectsHasEmptyMaps)
    "ReviewSessionEffectsTests.setPendingAddsEntry", TestBody.Sync (sync ReviewSessionEffectsTests.setPendingAddsEntry)
    "ReviewSessionEffectsTests.resolvePendingFiresCallback", TestBody.Sync (sync ReviewSessionEffectsTests.resolvePendingFiresCallback)
    "ReviewSessionEffectsTests.resolvePendingUnknownIdReturnsFalse", TestBody.Sync (sync ReviewSessionEffectsTests.resolvePendingUnknownIdReturnsFalse)
    "ReviewSessionEffectsTests.disposeSessionTreeTerminatesAll", TestBody.Sync (sync ReviewSessionEffectsTests.disposeSessionTreeTerminatesAll)
    "ReviewSessionRegistryTests.run", TestBody.Sync (sync ReviewSessionRegistryTests.run)
    "ReviewSessionQueryTests.run", TestBody.Sync (sync ReviewSessionQueryTests.run)
    "ReviewPromptsFormatTests.run", TestBody.Sync (sync ReviewPromptsFormatTests.run)
    "ReviewTests.disposeSessionTreeTerminatesAll", TestBody.Sync (sync ReviewTestsReplay.disposeSessionTreeTerminatesAll)
    "EventLogFoldTests.run", TestBody.Sync (sync EventLogFoldTests.run)
    "EventLogCodecTests.run", TestBody.Sync (sync EventLogCodecTests.run)
    "EventLogRuntimeTests.run", TestBody.Async EventLogRuntimeTests.run
    "ReviewTests.inferReviewTaskFromTexts'", TestBody.Sync (sync ReviewTestsReplay.inferReviewTaskFromTexts')
    "ReviewTests.parseFrontMatterScalars'", TestBody.Sync (sync ReviewTestsReplay.parseFrontMatterScalars')
    "KernelPromptSpecsReview.yamlFrontMatterRoundTrip", TestBody.Sync (sync KernelPromptSpecsReview.yamlFrontMatterRoundTrip)
    "ReviewTests.doubleCheckAnchorReplay", TestBody.Sync (sync ReviewTestsPrompts.doubleCheckAnchorReplay)
    "ReviewTests.doubleCheckPromptFormat", TestBody.Sync (sync ReviewTestsPrompts.doubleCheckPromptFormat)
    "ReviewTests.reviewerPromptFormat", TestBody.Sync (sync ReviewTestsPrompts.reviewerPromptFormat)
    "ReviewTests.muxReviewerVerdictPromptFormat", TestBody.Sync (sync ReviewTestsPrompts.muxReviewerVerdictPromptFormat)
    "ReviewTests.muxPreReviewVerdictPromptFormat", TestBody.Sync (sync ReviewTestsPrompts.muxPreReviewVerdictPromptFormat)
    "ReviewTests.reviewInstructionsFrontMatter", TestBody.Sync (sync ReviewTestsPrompts.reviewInstructionsFrontMatter)
    "AgentTests.canUse'", TestBody.Sync (sync AgentTests.canUse')
    "AgentTests.canUseMatrix", TestBody.Sync (sync AgentTests.canUseMatrix)
    "AgentTests.deniedTools'", TestBody.Sync (sync AgentTests.deniedTools')
    "AgentNudgeSpecs.decision", TestBody.Sync (sync AgentNudgeSpecs.decision)
    "AgentNudgeSpecs.dedupFromIntegral", TestBody.Sync (sync AgentNudgeSpecs.dedupFromIntegral)
    "AgentNudgeSpecs.decideNudge'", TestBody.Sync (sync AgentNudgeSpecs.decideNudge')
    "AgentNudgeSpecs.selectPrompt", TestBody.Sync (sync AgentNudgeSpecs.selectPrompt)
    "AgentNudgeSpecs.submitReviewWipNudgeDedup", TestBody.Sync (sync AgentNudgeSpecsWip.submitReviewWipNudgeDedup)
    "AgentNudgeSpecs.decodeTodosOpenItems", TestBody.Sync (sync AgentNudgeSpecsDecode.decodeTodosOpenItems)
    "AgentNudgeSpecsWip.submitReviewWipNudgeDedup", TestBody.Sync (sync AgentNudgeSpecsWip.submitReviewWipNudgeDedup)
    "AgentNudgeSpecsDecode.decodeTodosOpenItems", TestBody.Sync (sync AgentNudgeSpecsDecode.decodeTodosOpenItems)
    "NudgeRetryProgressTests.run", TestBody.Sync (sync NudgeRetryProgressTests.run)
    "NudgeTodoStatusTests.run", TestBody.Sync (sync NudgeTodoStatusTests.run)
    "NudgeEventSourcingTests.run", TestBody.Sync (sync NudgeEventSourcingTests.run)
    "KernelHelpersTests.run", TestBody.Sync (sync KernelHelpersTests.run)
    "ReviewSessionStateMachineTests.run", TestBody.Sync (sync ReviewSessionStateMachineTests.run)
    "HostToolsTests.run", TestBody.Sync (sync HostToolsTests.run)
    "ToolPermissionTests.run", TestBody.Sync (sync ToolPermissionTests.run)
    "SubagentPromptsTests.run", TestBody.Sync (sync SubagentPromptsTests.run)
    "SubagentIntentsTests.run", TestBody.Sync (sync SubagentIntentsTests.run)
    "SubagentDispatcherTests.run", TestBody.Async SubagentDispatcherTests.run
    "KernelTests.headTail'", TestBody.Sync (sync KernelTests.headTail')
    "KernelTests.stripLexer'", TestBody.Sync (sync KernelTests.stripLexer')
    "KernelTests.dedup'", TestBody.Sync (sync KernelTests.dedup')
    "KernelTests.jsBoundary'", TestBody.Sync (sync KernelTests.jsBoundary')
    "KernelPromptSpecs.hostKernel'", TestBody.Sync (sync KernelPromptSpecs.hostKernel')
    "DynFieldTests.run", TestBody.Sync (sync DynFieldTests.run)
    "KernelPromptSpecs.toolCatalogCentralized", TestBody.Sync (sync KernelPromptSpecs.toolCatalogCentralized)
    "KernelPromptSpecs.subagentDispatch", TestBody.Sync (sync KernelPromptSpecs.subagentDispatch)
    "KernelPromptSpecs.mimocodeFormatPromptAppendsAgentReportTail", TestBody.Sync (sync KernelPromptSpecs.mimocodeFormatPromptAppendsAgentReportTail)
    "KernelPromptSpecs.subagentJoinReports", TestBody.Sync (sync KernelPromptSpecs.subagentJoinReports)
    "KernelTests.dynDeleteKey", TestBody.Sync (sync KernelTests.dynDeleteKey)
    "KernelPromptSpecs.loopMessagesShared", TestBody.Sync (sync KernelPromptSpecs.loopMessagesShared)
    "KernelPromptSpecs.reviewerVerdictPromptsShared", TestBody.Sync (sync KernelPromptSpecs.reviewerVerdictPromptsShared)
    "KernelPromptSpecs.reviewResultFormattingShared", TestBody.Sync (sync KernelPromptSpecs.reviewResultFormattingShared)
    "KernelPromptSpecs.reviewVerdictDecode", TestBody.Sync (sync KernelPromptSpecs.reviewVerdictDecode)
    "KernelPromptSpecs.reviewDecisionPolicy", TestBody.Sync (sync KernelPromptSpecs.reviewDecisionPolicy)
    "KernelPromptSpecs.reviewMarkdownCodec", TestBody.Sync (sync KernelPromptSpecs.reviewMarkdownCodec)
    "KernelPromptSpecs.executorSummarizerNoExitStatus", TestBody.Sync (sync KernelPromptSpecs.executorSummarizerNoExitStatus)
    "KernelPromptSpecs.domainErrorsShared", TestBody.Sync (sync KernelPromptSpecs.domainErrorsShared)
    "FuzzyTests.grepDetect", TestBody.Sync (sync FuzzyTests.grepDetect)
    "FuzzyTests.iteratorRoundTrip", TestBody.Sync (sync FuzzyTests.iteratorRoundTrip)
    "FuzzyTests.finderConversion", TestBody.Sync (sync FuzzyTests.finderConversion)
    "FuzzyTests.formatFull", TestBody.Sync (sync FuzzyTests.formatFull)
    "FuzzyTests.fuzzyFallbackNotice", TestBody.Sync (sync FuzzyTests.fuzzyFallbackNotice)
    "FuzzyTests.finderCacheConcurrencyRace", TestBody.Async FuzzyTests.finderCacheConcurrencyRace
    "FuzzyTests.grepMaxMatchesPerFileRespectsPageSize", TestBody.Async FuzzyTests.grepMaxMatchesPerFileRespectsPageSize
    "FuzzyTests.findPagingWhenTotalMatchedIsNone", TestBody.Async FuzzyTests.findPagingWhenTotalMatchedIsNone
    "FuzzyTests.scanTimeoutConfigurable", TestBody.Sync (sync FuzzyTests.scanTimeoutConfigurable)
    "FuzzyTests.iteratorCounterUniqueness", TestBody.Sync (sync FuzzyTests.iteratorCounterUniqueness)
    "FuzzyTests.grepMultiPropagatesErrorAndSafety", TestBody.Async FuzzyTests.grepMultiPropagatesErrorAndSafety
    "FuzzyTestsPaging.findPagingDefault", TestBody.Sync (sync FuzzyTestsPaging.findPagingDefault)
    "FuzzyTestsPaging.emptyIteratorNotRendered", TestBody.Sync (sync FuzzyTestsPaging.emptyIteratorNotRendered)
    "FuzzyTestsPaging.totalMatchedSemantics", TestBody.Sync (sync FuzzyTestsPaging.totalMatchedSemantics)
    "FuzzyTestsPaging.grepOutputNotices", TestBody.Sync (sync FuzzyTestsPaging.grepOutputNotices)
    "FuzzyTestsPaging.iteratorNamespaceConstants", TestBody.Sync (sync FuzzyTestsPaging.iteratorNamespaceConstants)
    "FuzzyTestsPaging.iteratorStoreStronglyTyped", TestBody.Sync (sync FuzzyTestsPaging.iteratorStoreStronglyTyped)
    "FuzzyTestsPaging.runWithFinderSharedPipeline", TestBody.Sync (sync FuzzyTestsPaging.runWithFinderSharedPipeline)
    "FuzzyTestsPaging.resolveStoreRequiresInjection", TestBody.Sync (sync FuzzyTestsPaging.resolveStoreRequiresInjection)
    "FuzzyTestsPaging.emptyIteratorTreatedAsAbsent", TestBody.Sync (sync FuzzyTestsPaging.emptyIteratorTreatedAsAbsent)
    "ShellTests.webApiFetchInit", TestBody.Sync (sync ShellTests.webApiFetchInit)
    "ShellTests.webApiResponseMethodCall", TestBody.Sync (sync ShellTests.webApiResponseMethodCall)
    "ShellTests.webApiKeyValidation", TestBody.Sync (sync ShellTests.webApiKeyValidation)
    "ShellTests.executorMapping", TestBody.Sync (sync ShellTests.executorMapping)
    "ShellTestsFormat.safetyWarning", TestBody.Sync (sync ShellTestsFormat.safetyWarning)
    "ShellTests.capsFileShape", TestBody.Sync (sync ShellTests.capsFileShape)
    "ShellTests.capsFileSizeLimit", TestBody.Sync (sync ShellTests.capsFileSizeLimit)
    "ShellTests.stripHeadTailPipesOutsideQuotes", TestBody.Sync (sync ShellTests.stripHeadTailPipesOutsideQuotes)
    "ShellTests.stripHeadTailPipesHeadTailChain", TestBody.Sync (sync ShellTests.stripHeadTailPipesHeadTailChain)
    "ShellTestsFormat.ollamaFormat", TestBody.Sync (sync ShellTestsFormat.ollamaFormat)
    "ShellTestsFormat.webApiSearchFormat", TestBody.Sync (sync ShellTestsFormat.webApiSearchFormat)
    "ShellTestsFormat.summarizerInputCap", TestBody.Sync (sync ShellTestsFormat.summarizerInputCap)
    "ShellTestsFormat.executorToolResponseFormatting", TestBody.Sync (sync ShellTestsFormat.executorToolResponseFormatting)
    "ShellTestsFormat.summarizerPromptOmitsReturnValue", TestBody.Sync (sync ShellTestsFormat.summarizerPromptOmitsReturnValue)
    "ShellTestsFormat.formatFetchResponseAllFields", TestBody.Sync (sync ShellTestsFormat.formatFetchResponseAllFields)
    "ShellTestsFormat.formatFetchResponseOnlyTitle", TestBody.Sync (sync ShellTestsFormat.formatFetchResponseOnlyTitle)
    "ShellTestsFormat.formatFetchResponseOnlyContent", TestBody.Sync (sync ShellTestsFormat.formatFetchResponseOnlyContent)
    "ShellTestsFormat.formatFetchResponseAllNone", TestBody.Sync (sync ShellTestsFormat.formatFetchResponseAllNone)
    "ShellTestsFormat.formatFetchResponseEmptyTitleOmitted", TestBody.Sync (sync ShellTestsFormat.formatFetchResponseEmptyTitleOmitted)
    "ShellTests.readDirectoryListing", TestBody.Async ShellTests.readDirectoryListing
    "ShellTests.ensureJavascriptProjectRepairsModuleType", TestBody.Async ShellTests.ensureJavascriptProjectRepairsModuleType
    "ShellTests.rewriteJavascriptRelativeImports", TestBody.Async ShellTests.rewriteJavascriptRelativeImports
    "DynTests.nullish", TestBody.Sync (sync DynTests.nullish)
    "DedupTests.run", TestBody.Sync (sync DedupTests.run)
    "DelegateTests.run", TestBody.Sync (sync DelegateTests.run)
    "DelegateToolsCodecTests.run", TestBody.Sync (sync DelegateToolsCodecTests.run)
    "PatchParserTests.run", TestBody.Sync (sync PatchParserTests.run)
    "ResolveAiSettingsTests.run", TestBody.Sync (sync ResolveAiSettingsTests.run)
    "IntegrationPluginTests.run", TestBody.Async IntegrationPluginTests.run
    "IntegrationEventTests.run", TestBody.Async IntegrationEventTests.run
    "IntegrationDedupTests.run", TestBody.Async IntegrationDedupTests.run
    "IntegrationOpencodeReviewSpecs.run", TestBody.Async IntegrationOpencodeReviewSpecs.run
    "IntegrationChatTests.run", TestBody.Async IntegrationChatTests.run
    "IntegrationChatTestsSubagent.run", TestBody.Async IntegrationChatTestsSubagent.run
    "OpencodeSessionLifecycleTests.childIdleDoesNotAbortParent", TestBody.Async OpencodeSessionLifecycleTests.childIdleDoesNotAbortParent
    "LoopMessagesTests.run", TestBody.Sync (sync LoopMessagesTests.run)
    "MessagingTests.run", TestBody.Sync (sync MessagingTests.run)
    "WorkBacklogTests.run", TestBody.Sync (sync WorkBacklogTests.run)
    "MethodologyTests.run", TestBody.Sync (sync MethodologyTests.run)
    "MethodologyRegistryTests.run", TestBody.Sync (sync MethodologyRegistryTests.run)
    "ToolCatalogRegistryTests.run", TestBody.Sync (sync ToolCatalogRegistryTests.run)
    "TitleFetchGuardTests.signature", TestBody.Sync (sync TitleFetchGuardTests.signature)
    "TitleFetchGuardTests.wrap", TestBody.Sync (sync TitleFetchGuardTests.wrap)
    "TitleFetchGuardTests.detect", TestBody.Sync (sync TitleFetchGuardTests.detect)
    "TitleFetchGuardTests.tryWrapString", TestBody.Sync (sync TitleFetchGuardTests.tryWrapString)
    "TitleFetchGuardTests.rewriteStringContent", TestBody.Sync (sync TitleFetchGuardTests.rewriteStringContent)
    "TitleFetchGuardTests.rewriteArrayContent", TestBody.Sync (sync TitleFetchGuardTests.rewriteArrayContent)
    "TitleFetchGuardTests.skipProbeMessage", TestBody.Sync (sync TitleFetchGuardTests.skipProbeMessage)
    "ToolCatalogClassificationTests.run", TestBody.Sync (sync ToolCatalogClassificationTests.run)
    "ToolOutputInfoTests.run", TestBody.Sync (sync ToolOutputInfoTests.run)
    "MessageTransformPolicyTests.run", TestBody.Async MessageTransformPolicyTests.run
    "SembleInjectionTests.run", TestBody.Sync (sync SembleInjectionTests.run)
    "SembleReviewerInjectionTests.testSembleInjectsForReviewer", TestBody.Async testSembleInjectsForReviewer
    "ExecutorKernelTests.run", TestBody.Sync (sync ExecutorKernelTests.run)
    "ToolExecuteTests.run", TestBody.Sync (sync ToolExecuteTests.run)
    "TreeSitterKernelTests.run", TestBody.Sync (sync TreeSitterKernelTests.run)
    "ConfigTests.run", TestBody.Sync (sync ConfigTests.run)
    "JsonSchemaBuildersTests.run", TestBody.Sync (sync JsonSchemaBuildersTests.run)
    "ExecutorStripTests.run", TestBody.Sync (sync ExecutorStripTests.run)
    "ExecutorTests.run", TestBody.Async (fun () -> ExecutorTests.run ())
    "WarnTddKernelFactsTests.run", TestBody.Sync (sync WarnTddKernelFactsTests.run)
    "WarnTddOpencodeEnforcementTests.run", TestBody.Async WarnTddOpencodeEnforcementTests.run
    "WarnTddMuxEnforcementTests.run", TestBody.Async WarnTddMuxEnforcementTests.run
    "WarnTddOmpEnforcementTests.run", TestBody.Sync (sync WarnTddOmpEnforcementTests.run)
    "EventDrivenHarnessDemo.run", TestBody.Async (fun () -> EventDrivenHarnessDemo.run ())
    ]
    @ TestsEntriesFallback.tailTestEntries ()
    @ fallbackTestEntries()
    @ TestsEntriesFuzzy.fuzzyTestEntries()
    @ TestsEntriesDomain.domainTestEntries()
    @ TestsEntriesCoverage.coverageTestEntries()
