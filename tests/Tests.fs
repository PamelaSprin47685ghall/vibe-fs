module VibeFs.Tests.Tests

open Fable.Core
open VibeFs.Tests.Assert
open VibeFs.Tests.ReviewTests
open VibeFs.Tests.AgentTests
open VibeFs.Tests.KernelTests
open VibeFs.Tests.FuzzyTests
open VibeFs.Tests.ShellTests
open VibeFs.Tests.DynTests
open VibeFs.Tests.DelegateTests
open VibeFs.Tests.ResolveAiSettingsTests
open VibeFs.Tests.IntegrationPluginTests
open VibeFs.Tests.IntegrationEventTests
open VibeFs.Tests.IntegrationDedupTests
open VibeFs.Tests.IntegrationToolTests
open VibeFs.Tests.IntegrationChatTests
open VibeFs.Tests.MagicTests
open VibeFs.Tests.MethodologyTests
open VibeFs.Tests.KnowledgeGraphTests
open VibeFs.Tests.KnowledgeGraphFileTests
open VibeFs.Tests.KnowledgeGraphKernelTests
open VibeFs.Tests.TitleFetchGuardTests

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
    "AgentTests.decision", Sync (sync AgentTests.decision)
    "AgentTests.updateState", Sync (sync AgentTests.updateState)
    "AgentTests.coordinatorRuntime", Sync (sync AgentTests.coordinatorRuntime)
    "AgentTests.shouldSuppress'", Sync (sync AgentTests.shouldSuppress')
    "AgentTests.decideNudge'", Sync (sync AgentTests.decideNudge')
    "AgentTests.decodeLastAssistantNudge", Sync (sync AgentTests.decodeLastAssistantNudge)
    "KernelTests.headTail'", Sync (sync KernelTests.headTail')
    "KernelTests.stripLexer'", Sync (sync KernelTests.stripLexer')
    "KernelTests.dedup'", Sync (sync KernelTests.dedup')
    "KernelTests.jsBoundary'", Sync (sync KernelTests.jsBoundary')
    "KernelTests.hostKernel'", Sync (sync KernelTests.hostKernel')
    "KernelTests.knowledgeGraphFetchAnswer", Sync (sync KernelTests.knowledgeGraphFetchAnswer)
    "KernelTests.knowledgeGraphDraftArrayParsing", Sync (sync KernelTests.knowledgeGraphDraftArrayParsing)
    "KernelTests.toolCatalogCentralized", Sync (sync KernelTests.toolCatalogCentralized)
    "KernelTests.hostToolsKnowledgeGraphNames", Sync (sync KernelTests.hostToolsKnowledgeGraphNames)
    "KernelTests.subagentDispatch", Sync (sync KernelTests.subagentDispatch)
    "KernelTests.subagentJoinReports", Sync (sync KernelTests.subagentJoinReports)
    "KernelTests.dynDeleteKey", Sync (sync KernelTests.dynDeleteKey)
    "KernelTests.loopMessagesShared", Sync (sync KernelTests.loopMessagesShared)
    "KernelTests.reviewerVerdictPromptsShared", Sync (sync KernelTests.reviewerVerdictPromptsShared)
    "KernelTests.reviewResultFormattingShared", Sync (sync KernelTests.reviewResultFormattingShared)
    "KernelTests.domainErrorsShared", Sync (sync KernelTests.domainErrorsShared)
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
    "ShellTests.ollamaFormat", Sync (sync ShellTests.ollamaFormat)
    "ShellTests.summarizerInputCap", Sync (sync ShellTests.summarizerInputCap)
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
        let runnableTests = selectedTests args
        if List.isEmpty runnableTests then
            printfn "No tests matched selectors: %A" args
            return 1
        else
            for (label, body) in runnableTests do
                match body with
                | Sync f -> let _ = timed label f in ()
                | Async f -> do! timedAsync label f
            return summary ()
    }
