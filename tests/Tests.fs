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

let runAll (_args: string array) : JS.Promise<int> =
    async {
        ReviewTests.transition' ()
        ReviewTests.registry ()
        ReviewTests.resultMapping ()
        ReviewTests.reviewerLoop ()
        ReviewTests.runtime ()
        ReviewTests.promptPartsBranches ()
        ReviewTests.resolvePendingClearsSuppressor ()
        ReviewTests.disposeSessionTreeTerminatesAll ()
        AgentTests.canUse' ()
        AgentTests.canUseMatrix ()
        AgentTests.deniedTools' ()
        AgentTests.decision ()
        AgentTests.updateState ()
        AgentTests.coordinatorRuntime ()
        AgentTests.shouldSuppress' ()
        KernelTests.headTail' ()
        KernelTests.dedup' ()
        KernelTests.jsBoundary' ()
        KernelTests.hostKernel' ()
        KernelTests.wikiFetchAnswer ()
        KernelTests.wikiDraftArrayParsing ()
        KernelTests.toolCatalogCentralized ()
        KernelTests.hostToolsWikiNames ()
        KernelTests.subagentDispatch ()
        KernelTests.subagentJoinReports ()
        KernelTests.dynDeleteKey ()
        KernelTests.loopMessagesShared ()
        KernelTests.reviewerVerdictPromptsShared ()
        KernelTests.reviewResultFormattingShared ()
        KernelTests.domainErrorsShared ()
        FuzzyTests.grepDetect ()
        FuzzyTests.iteratorRoundTrip ()
        FuzzyTests.finderConversion ()
        FuzzyTests.formatFull ()
        FuzzyTests.fuzzyFallbackNotice ()
        FuzzyTests.findPagingDefault ()
        FuzzyTests.emptyIteratorNotRendered ()
        FuzzyTests.totalMatchedSemantics ()
        FuzzyTests.iteratorNamespaceConstants ()
        FuzzyTests.iteratorStoreStronglyTyped ()
        FuzzyTests.runWithFinderSharedPipeline ()
        ShellTests.ollamaFetchInit ()
        ShellTests.ollamaResponseMethodCall ()
        ShellTests.ollamaApiKeyValidation ()
        ShellTests.executorMapping ()
        ShellTests.safetyWarning ()
        ShellTests.capsFileShape ()
        ShellTests.capsContextFormat ()
        ShellTests.capsFileSizeLimit ()
        ShellTests.ollamaFormat ()
        ShellTests.summarizerInputCap ()
        do! ShellTests.readDirectoryListing ()
        do! ShellTests.ensureJavascriptProjectRepairsModuleType ()
        do! ShellTests.wikiPortRangeSpec ()
        do! ShellTests.wikiPortSerialSpec ()
        DynTests.nullish ()
        DelegateTests.run ()
        ResolveAiSettingsTests.run ()
        do! IntegrationPluginTests.run () |> Async.AwaitPromise
        do! IntegrationEventTests.run () |> Async.AwaitPromise
        do! IntegrationDedupTests.run () |> Async.AwaitPromise
        do! IntegrationToolTests.run () |> Async.AwaitPromise
        do! IntegrationChatTests.run () |> Async.AwaitPromise
        do! WikiTests.run () |> Async.AwaitPromise
        do! WikiFileTests.run () |> Async.AwaitPromise
        do! WikiKernelTests.run () |> Async.AwaitPromise
        MagicTests.run ()
        return summary ()
    }
    |> Async.StartAsPromise
