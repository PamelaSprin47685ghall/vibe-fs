module Wanxiangshu.Tests.TestsEntriesCore

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ReviewTests
open Wanxiangshu.Tests.ReviewTestsReplay
open Wanxiangshu.Tests.ReviewTestsPrompts
open Wanxiangshu.Tests.AgentTests
open Wanxiangshu.Tests.AgentNudgeSpecs
open Wanxiangshu.Tests.AgentNudgeSpecsWip
open Wanxiangshu.Tests.AgentNudgeSpecsDecode
open Wanxiangshu.Tests.KernelTests
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
open Wanxiangshu.Tests.WorkBacklogTests
open Wanxiangshu.Tests.MethodologyTests
open Wanxiangshu.Tests.KnowledgeGraphTests
open Wanxiangshu.Tests.KnowledgeGraphFileTests
open Wanxiangshu.Tests.KnowledgeGraphKernelTests
open Wanxiangshu.Tests.TitleFetchGuardTests
open Wanxiangshu.Tests.ReviewReplaySyncTests
open Wanxiangshu.Tests.CapsSynthCommonTests
open Wanxiangshu.Tests.CapsFileCacheTests
open Wanxiangshu.Tests.SubagentPromptBuildTests
open Wanxiangshu.Tests.SubagentSpawnTests
open Wanxiangshu.Tests.WebToolsCodecTests
open Wanxiangshu.Tests.ReviewToolsCodecTests
open Wanxiangshu.Tests.KnowledgeGraphToolsCodecTests
open Wanxiangshu.Tests.ExecutorToolsCodecTests
open Wanxiangshu.Tests.ToolArgsDecodeTests
open Wanxiangshu.Tests.ToolResultWireTests
open Wanxiangshu.Tests.SubagentToolExecuteTests
open Wanxiangshu.Tests.FileToolsCodecTests
open Wanxiangshu.Tests.FuzzyToolsCodecTests
open Wanxiangshu.Tests.WorkBacklogToolsCodecTests
open Wanxiangshu.Tests.PatchToolsCodecTests
open Wanxiangshu.Tests.HostMessagePartCodecTests
open Wanxiangshu.Tests.MessagingPartCodecTests
open Wanxiangshu.Tests.ToolContextCodecTests
open Wanxiangshu.Tests.OpencodeContextCodecTests
open Wanxiangshu.Tests.OpencodeSessionPromptCodecTests
open Wanxiangshu.Tests.OpencodeSessionSpawnCodecTests
open Wanxiangshu.Tests.SessionIoPromptBodyTests
open Wanxiangshu.Tests.OpencodeAgentConfigCodecTests
open Wanxiangshu.Tests.OpencodeSessionEventCodecTests
open Wanxiangshu.Tests.MuxAiSettingsCodecTests
open Wanxiangshu.Tests.MuxAiSettingsIntegrationTests
open Wanxiangshu.Tests.AgentConfigApplyTests
open Wanxiangshu.Tests.KnowledgeGraphWorkflowTests
open Wanxiangshu.Tests.KnowledgeGraphBookkeeperLaunchTests
open Wanxiangshu.Tests.KnowledgeGraphMaintenanceRunTests
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
open Wanxiangshu.Tests.OmpKnowledgeGraphRuntimeTests
open Wanxiangshu.Tests.OmpSessionLifecycleTests
open Wanxiangshu.Tests.OmpPluginCoreTests
open Wanxiangshu.Tests.OmpTitleFetchGuardTests
open Wanxiangshu.Tests.OmpMagicTodoTests
open Wanxiangshu.Tests.OmpPluginCoreIntegrationTests
open Wanxiangshu.Tests.SubagentIoTests
open Wanxiangshu.Tests.TestsTestBody

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
