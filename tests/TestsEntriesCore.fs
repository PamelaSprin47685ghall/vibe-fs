module VibeFs.Tests.TestsEntriesCore

open VibeFs.Tests.Assert
open VibeFs.Tests.ReviewTests
open VibeFs.Tests.ReviewTestsReplay
open VibeFs.Tests.ReviewTestsPrompts
open VibeFs.Tests.AgentTests
open VibeFs.Tests.AgentNudgeSpecs
open VibeFs.Tests.AgentNudgeSpecsWip
open VibeFs.Tests.AgentNudgeSpecsDecode
open VibeFs.Tests.KernelTests
open VibeFs.Tests.KernelPromptSpecs
open VibeFs.Tests.FuzzyTests
open VibeFs.Tests.FuzzyTestsPaging
open VibeFs.Tests.ShellTests
open VibeFs.Tests.ShellTestsFormat
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
open VibeFs.Tests.IntegrationChatTestsSubagent
open VibeFs.Tests.WorkBacklogTests
open VibeFs.Tests.MethodologyTests
open VibeFs.Tests.KnowledgeGraphTests
open VibeFs.Tests.KnowledgeGraphFileTests
open VibeFs.Tests.KnowledgeGraphKernelTests
open VibeFs.Tests.TitleFetchGuardTests
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

let coreTestEntries () : (string * TestBody) list =
    [
    "ReviewTests.transition'", Sync (sync ReviewTests.transition')
    "ReviewTests.registry", Sync (sync ReviewTests.registry)
    "ReviewTests.resultMapping", Sync (sync ReviewTests.resultMapping)
    "ReviewTests.reviewerLoop", Sync (sync ReviewTests.reviewerLoop)
    "ReviewTests.runtime", Sync (sync ReviewTests.runtime)
    "ReviewTests.promptPartsBranches", Sync (sync ReviewTests.promptPartsBranches)
    "ReviewTests.resolvePendingClearsSuppressor", Sync (sync ReviewTests.resolvePendingClearsSuppressor)
    "ReviewTests.disposeSessionTreeTerminatesAll", Sync (sync ReviewTestsReplay.disposeSessionTreeTerminatesAll)
    "ReviewTests.inferReviewTaskFromTexts'", Sync (sync ReviewTestsReplay.inferReviewTaskFromTexts')
    "ReviewTests.parseFrontMatterScalars'", Sync (sync ReviewTestsReplay.parseFrontMatterScalars')
    "KernelPromptSpecsReview.yamlFrontMatterRoundTrip", Sync (sync KernelPromptSpecsReview.yamlFrontMatterRoundTrip)
    "ReviewTests.doubleCheckAnchorReplay", Sync (sync ReviewTestsPrompts.doubleCheckAnchorReplay)
    "ReviewTests.doubleCheckPromptFormat", Sync (sync ReviewTestsPrompts.doubleCheckPromptFormat)
    "ReviewTests.reviewerPromptFormat", Sync (sync ReviewTestsPrompts.reviewerPromptFormat)
    "ReviewTests.muxReviewerVerdictPromptFormat", Sync (sync ReviewTestsPrompts.muxReviewerVerdictPromptFormat)
    "ReviewTests.muxPreReviewVerdictPromptFormat", Sync (sync ReviewTestsPrompts.muxPreReviewVerdictPromptFormat)
    "ReviewTests.reviewInstructionsFrontMatter", Sync (sync ReviewTestsPrompts.reviewInstructionsFrontMatter)
    "AgentTests.canUse'", Sync (sync AgentTests.canUse')
    "AgentTests.canUseMatrix", Sync (sync AgentTests.canUseMatrix)
    "AgentTests.deniedTools'", Sync (sync AgentTests.deniedTools')
    "AgentNudgeSpecs.decision", Sync (sync AgentNudgeSpecs.decision)
    "AgentNudgeSpecs.updateState", Sync (sync AgentNudgeSpecs.updateState)
    "AgentNudgeSpecs.coordinatorRuntime", Sync (sync AgentNudgeSpecs.coordinatorRuntime)
    "AgentNudgeSpecs.shouldSuppress'", Sync (sync AgentNudgeSpecs.shouldSuppress')
    "AgentNudgeSpecs.decideNudge'", Sync (sync AgentNudgeSpecs.decideNudge')
    "AgentNudgeSpecs.decodeLastAssistantNudge", Sync (sync AgentNudgeSpecs.decodeLastAssistantNudge)
    "AgentNudgeSpecs.submitReviewWipNudgeDedup", Sync (sync AgentNudgeSpecsWip.submitReviewWipNudgeDedup)
    "AgentNudgeSpecs.decodeTodosOpenItems", Sync (sync AgentNudgeSpecsDecode.decodeTodosOpenItems)
    "AgentNudgeSpecsWip.submitReviewWipNudgeDedup", Sync (sync AgentNudgeSpecsWip.submitReviewWipNudgeDedup)
    "AgentNudgeSpecsDecode.decodeTodosOpenItems", Sync (sync AgentNudgeSpecsDecode.decodeTodosOpenItems)
    "KernelTests.headTail'", Sync (sync KernelTests.headTail')
    "KernelTests.stripLexer'", Sync (sync KernelTests.stripLexer')
    "KernelTests.dedup'", Sync (sync KernelTests.dedup')
    "KernelTests.jsBoundary'", Sync (sync KernelTests.jsBoundary')
    "KernelPromptSpecs.hostKernel'", Sync (sync KernelPromptSpecs.hostKernel')
    "KernelTests.knowledgeGraphFetchAnswer", Sync (sync KernelTests.knowledgeGraphFetchAnswer)
    "KernelPromptSpecs.toolCatalogCentralized", Sync (sync KernelPromptSpecs.toolCatalogCentralized)
    "KernelPromptSpecs.hostToolsKnowledgeGraphNames", Sync (sync KernelPromptSpecs.hostToolsKnowledgeGraphNames)
    "KernelPromptSpecs.subagentDispatch", Sync (sync KernelPromptSpecs.subagentDispatch)
    "KernelPromptSpecs.mimocodeFormatPromptAppendsAgentReportTail", Sync (sync KernelPromptSpecs.mimocodeFormatPromptAppendsAgentReportTail)
    "KernelPromptSpecs.subagentJoinReports", Sync (sync KernelPromptSpecs.subagentJoinReports)
    "KernelTests.dynDeleteKey", Sync (sync KernelTests.dynDeleteKey)
    "KernelPromptSpecs.loopMessagesShared", Sync (sync KernelPromptSpecs.loopMessagesShared)
    "KernelPromptSpecs.reviewerVerdictPromptsShared", Sync (sync KernelPromptSpecs.reviewerVerdictPromptsShared)
    "KernelPromptSpecs.reviewResultFormattingShared", Sync (sync KernelPromptSpecs.reviewResultFormattingShared)
    "KernelPromptSpecs.reviewVerdictDecode", Sync (sync KernelPromptSpecs.reviewVerdictDecode)
    "KernelPromptSpecs.reviewDecisionPolicy", Sync (sync KernelPromptSpecs.reviewDecisionPolicy)
    "KernelPromptSpecs.reviewMarkdownCodec", Sync (sync KernelPromptSpecs.reviewMarkdownCodec)
    "KernelPromptSpecs.executorSummarizerNoExitStatus", Sync (sync KernelPromptSpecs.executorSummarizerNoExitStatus)
    "KernelPromptSpecs.domainErrorsShared", Sync (sync KernelPromptSpecs.domainErrorsShared)
    "FuzzyTests.grepDetect", Sync (sync FuzzyTests.grepDetect)
    "FuzzyTests.iteratorRoundTrip", Sync (sync FuzzyTests.iteratorRoundTrip)
    "FuzzyTests.finderConversion", Sync (sync FuzzyTests.finderConversion)
    "FuzzyTests.formatFull", Sync (sync FuzzyTests.formatFull)
    "FuzzyTests.fuzzyFallbackNotice", Sync (sync FuzzyTests.fuzzyFallbackNotice)
    "FuzzyTestsPaging.findPagingDefault", Sync (sync FuzzyTestsPaging.findPagingDefault)
    "FuzzyTestsPaging.emptyIteratorNotRendered", Sync (sync FuzzyTestsPaging.emptyIteratorNotRendered)
    "FuzzyTestsPaging.totalMatchedSemantics", Sync (sync FuzzyTestsPaging.totalMatchedSemantics)
    "FuzzyTestsPaging.grepOutputNotices", Sync (sync FuzzyTestsPaging.grepOutputNotices)
    "FuzzyTestsPaging.iteratorNamespaceConstants", Sync (sync FuzzyTestsPaging.iteratorNamespaceConstants)
    "FuzzyTestsPaging.iteratorStoreStronglyTyped", Sync (sync FuzzyTestsPaging.iteratorStoreStronglyTyped)
    "FuzzyTestsPaging.runWithFinderSharedPipeline", Sync (sync FuzzyTestsPaging.runWithFinderSharedPipeline)
    "FuzzyTestsPaging.resolveStoreRequiresInjection", Sync (sync FuzzyTestsPaging.resolveStoreRequiresInjection)
    "FuzzyTestsPaging.emptyIteratorTreatedAsAbsent", Sync (sync FuzzyTestsPaging.emptyIteratorTreatedAsAbsent)
    "ShellTests.webApiFetchInit", Sync (sync ShellTests.webApiFetchInit)
    "ShellTests.webApiResponseMethodCall", Sync (sync ShellTests.webApiResponseMethodCall)
    "ShellTests.webApiKeyValidation", Sync (sync ShellTests.webApiKeyValidation)
    "ShellTests.executorMapping", Sync (sync ShellTests.executorMapping)
    "ShellTestsFormat.safetyWarning", Sync (sync ShellTestsFormat.safetyWarning)
    "ShellTests.capsFileShape", Sync (sync ShellTests.capsFileShape)
    "ShellTests.capsContextFormat", Sync (sync ShellTests.capsContextFormat)
    "ShellTests.capsFileSizeLimit", Sync (sync ShellTests.capsFileSizeLimit)
    "ShellTests.stripHeadTailPipesOutsideQuotes", Sync (sync ShellTests.stripHeadTailPipesOutsideQuotes)
    "ShellTests.stripHeadTailPipesHeadTailChain", Sync (sync ShellTests.stripHeadTailPipesHeadTailChain)
    "ShellTestsFormat.ollamaFormat", Sync (sync ShellTestsFormat.ollamaFormat)
    "ShellTestsFormat.webApiSearchFormat", Sync (sync ShellTestsFormat.webApiSearchFormat)
    "ShellTestsFormat.summarizerInputCap", Sync (sync ShellTestsFormat.summarizerInputCap)
    "ShellTestsFormat.executorToolResponseFormatting", Sync (sync ShellTestsFormat.executorToolResponseFormatting)
    "ShellTestsFormat.summarizerPromptOmitsReturnValue", Sync (sync ShellTestsFormat.summarizerPromptOmitsReturnValue)
    "ShellTests.readDirectoryListing", Async ShellTests.readDirectoryListing
    "ShellTests.ensureJavascriptProjectRepairsModuleType", Async ShellTests.ensureJavascriptProjectRepairsModuleType
    "ShellTests.rewriteJavascriptRelativeImports", Async ShellTests.rewriteJavascriptRelativeImports
    "ShellTests.knowledgeGraphPortRangeSpec", Async ShellTests.knowledgeGraphPortRangeSpec
    "ShellTests.knowledgeGraphPortSerialSpec", Async ShellTests.knowledgeGraphPortSerialSpec
    "DynTests.nullish", Sync (sync DynTests.nullish)
    "DelegateTests.run", Sync (sync DelegateTests.run)
    "DelegateToolsCodecTests.run", Sync (sync DelegateToolsCodecTests.run)
    "ResolveAiSettingsTests.run", Sync (sync ResolveAiSettingsTests.run)
    "IntegrationPluginTests.run", Async IntegrationPluginTests.run
    "IntegrationEventTests.run", Async IntegrationEventTests.run
    "IntegrationDedupTests.run", Async IntegrationDedupTests.run
    "IntegrationOpencodeReviewSpecs.run", Async IntegrationOpencodeReviewSpecs.run
    "IntegrationChatTests.run", Async IntegrationChatTests.run
    "IntegrationChatTestsSubagent.run", Async IntegrationChatTestsSubagent.run
    "KnowledgeGraphTests.run", Async KnowledgeGraphTests.run
    "KnowledgeGraphFileTests.run", Async KnowledgeGraphFileTests.run
    "KnowledgeGraphKernelTests.run", Async KnowledgeGraphKernelTests.run
    "WorkBacklogTests.run", Sync (sync WorkBacklogTests.run)
    "MethodologyTests.run", Sync (sync MethodologyTests.run)
    "TitleFetchGuardTests.signature", Sync (sync TitleFetchGuardTests.signature)
    "TitleFetchGuardTests.wrap", Sync (sync TitleFetchGuardTests.wrap)
    "TitleFetchGuardTests.detect", Sync (sync TitleFetchGuardTests.detect)
    "TitleFetchGuardTests.tryWrapString", Sync (sync TitleFetchGuardTests.tryWrapString)
    "TitleFetchGuardTests.rewriteStringContent", Sync (sync TitleFetchGuardTests.rewriteStringContent)
    "TitleFetchGuardTests.rewriteArrayContent", Sync (sync TitleFetchGuardTests.rewriteArrayContent)
    "TitleFetchGuardTests.skipProbeMessage", Sync (sync TitleFetchGuardTests.skipProbeMessage)
    ]
