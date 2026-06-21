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
open VibeFs.Tests.WikiTests
open VibeFs.Tests.WikiFileTests
open VibeFs.Tests.WikiKernelTests
open VibeFs.Tests.TitleFetchGuardTests
open VibeFs.Tests.IntegrationEditPlusSpecs

/// A test body: synchronous bodies run inline, asynchronous return a promise.
type private TestBody =
    | Sync of (unit -> unit)
    | Async of (unit -> JS.Promise<unit>)

/// Coerce any synchronous body into a unit-returning one.
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
    "KernelTests.wikiFetchAnswer", Sync (sync KernelTests.wikiFetchAnswer)
    "KernelTests.wikiDraftArrayParsing", Sync (sync KernelTests.wikiDraftArrayParsing)
    "KernelTests.toolCatalogCentralized", Sync (sync KernelTests.toolCatalogCentralized)
    "KernelTests.hostToolsWikiNames", Sync (sync KernelTests.hostToolsWikiNames)
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
    "ShellTests.wikiPortRangeSpec", Async ShellTests.wikiPortRangeSpec
    "ShellTests.wikiPortSerialSpec", Async ShellTests.wikiPortSerialSpec
    "DynTests.nullish", Sync (sync DynTests.nullish)
    "DelegateTests.run", Sync (sync DelegateTests.run)
    "ResolveAiSettingsTests.run", Sync (sync ResolveAiSettingsTests.run)
    "IntegrationPluginTests.run", Async IntegrationPluginTests.run
    "IntegrationEventTests.run", Async IntegrationEventTests.run
    "IntegrationDedupTests.run", Async IntegrationDedupTests.run
    "IntegrationToolTests.run", Async IntegrationToolTests.run
    "IntegrationChatTests.run", Async IntegrationChatTests.run
    "WikiTests.run", Async WikiTests.run
    "WikiFileTests.run", Async WikiFileTests.run
    "WikiKernelTests.run", Async WikiKernelTests.run
    "MagicTests.run", Sync (sync MagicTests.run)
    "TitleFetchGuardTests.signature", Sync (sync TitleFetchGuardTests.signature)
    "TitleFetchGuardTests.wrap", Sync (sync TitleFetchGuardTests.wrap)
    "TitleFetchGuardTests.detect", Sync (sync TitleFetchGuardTests.detect)
    "TitleFetchGuardTests.tryWrapString", Sync (sync TitleFetchGuardTests.tryWrapString)
    "TitleFetchGuardTests.rewriteStringContent", Sync (sync TitleFetchGuardTests.rewriteStringContent)
    "TitleFetchGuardTests.rewriteArrayContent", Sync (sync TitleFetchGuardTests.rewriteArrayContent)
    "TitleFetchGuardTests.skipProbeMessage", Sync (sync TitleFetchGuardTests.skipProbeMessage)
    "IntegrationEditPlusSpecs.run", Async IntegrationEditPlusSpecs.run
]

let runAll (_args: string array) : JS.Promise<int> =
    promise {
        for (label, body) in tests do
            match body with
            | Sync f -> let _ = timed label f in ()
            | Async f -> do! timedAsync label f
        return summary ()
    }
