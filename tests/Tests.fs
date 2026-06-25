module VibeFs.Tests.Tests

open Fable.Core
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
open VibeFs.Tests.ResolveAiSettingsTests
open VibeFs.Tests.IntegrationPluginTests
open VibeFs.Tests.IntegrationEventTests
open VibeFs.Tests.IntegrationDedupTests
open VibeFs.Tests.IntegrationToolTests
open VibeFs.Tests.IntegrationOpencodeReviewSpecs
open VibeFs.Tests.IntegrationChatTests
open VibeFs.Tests.MagicTests
open VibeFs.Tests.MethodologyTests
open VibeFs.Tests.KnowledgeGraphTests
open VibeFs.Tests.KnowledgeGraphFileTests
open VibeFs.Tests.KnowledgeGraphKernelTests
open VibeFs.Tests.TitleFetchGuardTests
open VibeFs.Tests.ArchitectureTests
open VibeFs.Tests.OmpKernelTests
open VibeFs.Tests.OmpSessionToolsTests
open VibeFs.Tests.OmpWebFetchTests
open VibeFs.Tests.OmpCapsTests
open VibeFs.Tests.OmpFuzzyTests
open VibeFs.Tests.OmpPluginTests
open VibeFs.Tests.OmpReviewTests
open VibeFs.Tests.OmpHelpersTests
open VibeFs.Tests.OmpRunnerTests
open VibeFs.Tests.OmpChildSessionTests
open VibeFs.Omp.Plugin

type private TestBody =
    | Sync of (unit -> unit)
    | Async of (unit -> JS.Promise<unit>)

let private sync (f: unit -> 'a) : unit -> unit = fun () -> ignore (f ())

let private tests : (string * TestBody) list = [
    "ReviewTests.transition'", Sync (sync ReviewTests.transition')
    "ReviewTests.registry", Sync (sync ReviewTests.registry)
    "ReviewTests.resultMapping", Sync (sync ReviewTests.resultMapping)
    "ReviewTests.reviewerLoop", Sync (sync ReviewTests.reviewerLoop)
    "ReviewTests.runtime", Sync (sync ReviewTests.runtime)
    "ReviewTests.promptPartsBranches", Sync (sync ReviewTests.promptPartsBranches)
    "ReviewTests.resolvePendingClearsSuppressor", Sync (sync ReviewTests.resolvePendingClearsSuppressor)
    "ReviewTests.disposeSessionTreeTerminatesAll", Sync (sync ReviewTests.disposeSessionTreeTerminatesAll)
    "ReviewTests.inferReviewTaskFromTexts'", Sync (sync ReviewTests.inferReviewTaskFromTexts')
    "ReviewTests.parseFrontMatterScalars'", Sync (sync ReviewTests.parseFrontMatterScalars')
    "ReviewTests.doubleCheckAnchorReplay", Sync (sync ReviewTests.doubleCheckAnchorReplay)
    "ReviewTests.doubleCheckPromptFormat", Sync (sync ReviewTests.doubleCheckPromptFormat)
    "ReviewTests.reviewerPromptFormat", Sync (sync ReviewTests.reviewerPromptFormat)
    "ReviewTests.muxReviewerVerdictPromptFormat", Sync (sync ReviewTests.muxReviewerVerdictPromptFormat)
    "ReviewTests.muxPreReviewVerdictPromptFormat", Sync (sync ReviewTests.muxPreReviewVerdictPromptFormat)
    "ReviewTests.reviewInstructionsFrontMatter", Sync (sync ReviewTests.reviewInstructionsFrontMatter)
    "AgentTests.canUse'", Sync (sync AgentTests.canUse')
    "AgentTests.canUseMatrix", Sync (sync AgentTests.canUseMatrix)
    "AgentTests.deniedTools'", Sync (sync AgentTests.deniedTools')
    "AgentNudgeSpecs.decision", Sync (sync AgentNudgeSpecs.decision)
    "AgentNudgeSpecs.updateState", Sync (sync AgentNudgeSpecs.updateState)
    "AgentNudgeSpecs.coordinatorRuntime", Sync (sync AgentNudgeSpecs.coordinatorRuntime)
    "AgentNudgeSpecs.shouldSuppress'", Sync (sync AgentNudgeSpecs.shouldSuppress')
    "AgentNudgeSpecs.decideNudge'", Sync (sync AgentNudgeSpecs.decideNudge')
    "AgentNudgeSpecs.decodeLastAssistantNudge", Sync (sync AgentNudgeSpecs.decodeLastAssistantNudge)
    "KernelTests.headTail'", Sync (sync KernelTests.headTail')
    "KernelTests.stripLexer'", Sync (sync KernelTests.stripLexer')
    "KernelTests.dedup'", Sync (sync KernelTests.dedup')
    "KernelTests.jsBoundary'", Sync (sync KernelTests.jsBoundary')
    "KernelPromptSpecs.hostKernel'", Sync (sync KernelPromptSpecs.hostKernel')
    "KernelTests.knowledgeGraphFetchAnswer", Sync (sync KernelTests.knowledgeGraphFetchAnswer)
    "KernelPromptSpecs.toolCatalogCentralized", Sync (sync KernelPromptSpecs.toolCatalogCentralized)
    "KernelPromptSpecs.hostToolsKnowledgeGraphNames", Sync (sync KernelPromptSpecs.hostToolsKnowledgeGraphNames)
    "KernelPromptSpecs.subagentDispatch", Sync (sync KernelPromptSpecs.subagentDispatch)
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
    "FuzzyTests.findPagingDefault", Sync (sync FuzzyTests.findPagingDefault)
    "FuzzyTests.emptyIteratorNotRendered", Sync (sync FuzzyTests.emptyIteratorNotRendered)
    "FuzzyTests.totalMatchedSemantics", Sync (sync FuzzyTests.totalMatchedSemantics)
    "FuzzyTests.grepOutputNotices", Sync (sync FuzzyTests.grepOutputNotices)
    "FuzzyTests.iteratorNamespaceConstants", Sync (sync FuzzyTests.iteratorNamespaceConstants)
    "FuzzyTests.iteratorStoreStronglyTyped", Sync (sync FuzzyTests.iteratorStoreStronglyTyped)
    "FuzzyTests.runWithFinderSharedPipeline", Sync (sync FuzzyTests.runWithFinderSharedPipeline)
    "FuzzyTests.emptyIteratorTreatedAsAbsent", Sync (sync FuzzyTests.emptyIteratorTreatedAsAbsent)
    "ShellTests.ollamaFetchInit", Sync (sync ShellTests.ollamaFetchInit)
    "ShellTests.ollamaResponseMethodCall", Sync (sync ShellTests.ollamaResponseMethodCall)
    "ShellTests.ollamaApiKeyValidation", Sync (sync ShellTests.ollamaApiKeyValidation)
    "ShellTests.executorMapping", Sync (sync ShellTests.executorMapping)
    "ShellTests.safetyWarning", Sync (sync ShellTests.safetyWarning)
    "ShellTests.capsFileShape", Sync (sync ShellTests.capsFileShape)
    "ShellTests.capsContextFormat", Sync (sync ShellTests.capsContextFormat)
    "ShellTests.capsFileSizeLimit", Sync (sync ShellTests.capsFileSizeLimit)
    "ShellTests.stripHeadTailPipesOutsideQuotes", Sync (sync ShellTests.stripHeadTailPipesOutsideQuotes)
    "ShellTests.stripHeadTailPipesHeadTailChain", Sync (sync ShellTests.stripHeadTailPipesHeadTailChain)
    "ShellTests.ollamaFormat", Sync (sync ShellTests.ollamaFormat)

    "ShellTests.executorToolResponseFormatting", Sync (sync ShellTests.executorToolResponseFormatting)
    "ShellTests.summarizerPromptOmitsReturnValue", Sync (sync ShellTests.summarizerPromptOmitsReturnValue)
    "ShellTests.readDirectoryListing", Async ShellTests.readDirectoryListing
    "ShellTests.ensureJavascriptProjectRepairsModuleType", Async ShellTests.ensureJavascriptProjectRepairsModuleType
    "ShellTests.rewriteJavascriptRelativeImports", Async ShellTests.rewriteJavascriptRelativeImports
    "ShellTests.knowledgeGraphPortRangeSpec", Async ShellTests.knowledgeGraphPortRangeSpec
    "ShellTests.knowledgeGraphPortSerialSpec", Async ShellTests.knowledgeGraphPortSerialSpec
    "DynTests.nullish", Sync (sync DynTests.nullish)
    "DelegateTests.run", Sync (sync DelegateTests.run)
    "ResolveAiSettingsTests.run", Sync (sync ResolveAiSettingsTests.run)
    "IntegrationPluginTests.run", Async IntegrationPluginTests.run
    "IntegrationEventTests.run", Async IntegrationEventTests.run
    "IntegrationDedupTests.run", Async IntegrationDedupTests.run
    "IntegrationToolTests.run", Async IntegrationToolTests.run
    "IntegrationOpencodeReviewSpecs.run", Async IntegrationOpencodeReviewSpecs.run
    "IntegrationChatTests.run", Async IntegrationChatTests.run
    "KnowledgeGraphTests.run", Async KnowledgeGraphTests.run
    "KnowledgeGraphFileTests.run", Async KnowledgeGraphFileTests.run
    "KnowledgeGraphKernelTests.run", Async KnowledgeGraphKernelTests.run
    "MagicTests.run", Sync (sync MagicTests.run)
    "MethodologyTests.run", Sync (sync MethodologyTests.run)
    "TitleFetchGuardTests.signature", Sync (sync TitleFetchGuardTests.signature)
    "TitleFetchGuardTests.wrap", Sync (sync TitleFetchGuardTests.wrap)
    "TitleFetchGuardTests.detect", Sync (sync TitleFetchGuardTests.detect)
    "TitleFetchGuardTests.tryWrapString", Sync (sync TitleFetchGuardTests.tryWrapString)
    "TitleFetchGuardTests.rewriteStringContent", Sync (sync TitleFetchGuardTests.rewriteStringContent)
    "TitleFetchGuardTests.rewriteArrayContent", Sync (sync TitleFetchGuardTests.rewriteArrayContent)
    "TitleFetchGuardTests.skipProbeMessage", Sync (sync TitleFetchGuardTests.skipProbeMessage)
    "ArchitectureTests.kernelBoundary", Sync (sync ArchitectureTests.kernelBoundary)
    "ArchitectureTests.kernelNoEmptyDefault", Sync (sync ArchitectureTests.kernelNoEmptyDefault)
    "ArchitectureTests.shellLayering", Sync (sync ArchitectureTests.shellLayering)
    "ArchitectureTests.fileBodyUnder300", Sync (sync ArchitectureTests.fileBodyUnder300)
    "ArchitectureTests.noDanglingMarkers", Sync (sync ArchitectureTests.noDanglingMarkers)
    "ArchitectureTests.noBuiltinDictionary", Sync (sync ArchitectureTests.noBuiltinDictionary)
    "ArchitectureTests.opencodeHookSchemaNoDirectZodImport", Sync (sync ArchitectureTests.opencodeHookSchemaNoDirectZodImport)
    "ArchitectureTests.noLegacyInjectedToolOutputMarkers", Sync (sync ArchitectureTests.noLegacyInjectedToolOutputMarkers)
    "ArchitectureTests.ompBoundary", Sync (sync ArchitectureTests.ompBoundary)
    "ArchitectureTests.ompNoOpencodeRef", Sync (sync ArchitectureTests.ompNoOpencodeRef)
    "ArchitectureTests.ompNoMuxRef", Sync (sync ArchitectureTests.ompNoMuxRef)
    "ArchitectureTests.ompNoEngineRef", Sync (sync ArchitectureTests.ompNoEngineRef)
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
    "OmpPluginTests.sessionStartStripsMainSessionTools", Async OmpPluginTests.sessionStartStripsMainSessionTools
    "OmpPluginTests.fuzzyDescriptionsMatchMuxWording", Sync (sync OmpPluginTests.fuzzyDescriptionsMatchMuxWording)
    "OmpPluginTests.readAssistantTextFromEntries", Sync (sync OmpPluginTests.readAssistantTextFromEntries)
    "OmpPluginTests.subagentPromptsContainKernelFragments", Sync (sync OmpPluginTests.subagentPromptsContainKernelFragments)
    "OmpPluginTests.executorToolSchemaFourFields", Async OmpPluginTests.executorToolSchemaFourFields
    "OmpPluginTests.browserErrorsWithoutBrowserHost", Async OmpPluginTests.browserErrorsWithoutBrowserHost
    "OmpPluginTests.reviewChildInitialPromptUsesReturnReviewer", Sync (sync OmpPluginTests.reviewChildInitialPromptUsesReturnReviewer)
    "OmpPluginTests.fuzzyGrepExcludeAnyOfLength2", Async OmpPluginTests.fuzzyGrepExcludeAnyOfLength2
    "OmpPluginTests.agentEndRunnerNudgeBeforeLoop", Async OmpPluginTests.agentEndRunnerNudgeBeforeLoop
    "OmpPluginTests.agentEndLoopNudgeWhenActive", Async OmpPluginTests.agentEndLoopNudgeWhenActive
    "OmpPluginTests.agentEndSkipsLoopNudgeWhenPendingMessages", Async OmpPluginTests.agentEndSkipsLoopNudgeWhenPendingMessages
    "OmpReviewTests.loopInputHandledMessageAndNotify", Async OmpReviewTests.loopInputHandledMessageAndNotify
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
]

let private matchesSelector (selectors: string array) (label: string) =
    selectors.Length = 0
    || selectors
       |> Array.exists (fun selector ->
           let trimmed = selector.Trim ()
           trimmed.Length > 0 && label.StartsWith trimmed)

let private selectedTests (selectors: string array) =
    tests |> List.filter (fun (label, _) -> matchesSelector selectors label)

let runAll (args: string array) : JS.Promise<int> =
    promise {
        clearFailuresForRun ()
        resetOmpPluginTestState ()
        let runnableTests = selectedTests args
        if List.isEmpty runnableTests then
            printfn "No tests matched selectors: %A" args
            return 1
        else
            for (label, body) in runnableTests do
                match body with
                | Sync f -> timed label f
                | Async f -> do! timedAsync label f
            return summary ()
    }
