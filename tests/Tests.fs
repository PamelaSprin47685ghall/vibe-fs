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

let runAll (_args: string array) : JS.Promise<int> =
    async {
        ReviewTests.transition' ()
        ReviewTests.registry ()
        ReviewTests.resultMapping ()
        ReviewTests.reviewerLoop ()
        ReviewTests.runtime ()
        AgentTests.canUse' ()
        AgentTests.deniedTools' ()
        AgentTests.decision ()
        AgentTests.updateState ()
        AgentTests.coordinator ()
        AgentTests.shouldSuppress' ()
        KernelTests.headTail' ()
        KernelTests.dedup' ()
        KernelTests.excludedDirs' ()
        KernelTests.jsBoundary' ()
        KernelTests.hostKernel' ()
        FuzzyTests.grepDetect ()
        FuzzyTests.iteratorRoundTrip ()
        FuzzyTests.finderConversion ()
        FuzzyTests.formatFull ()
        FuzzyTests.fuzzyFallbackNotice ()
        FuzzyTests.findPagingDefault ()
        FuzzyTests.totalMatchedSemantics ()
        ShellTests.ollamaFetchInit ()
        ShellTests.ollamaResponseMethodCall ()
        ShellTests.executorMapping ()
        ShellTests.safetyWarning ()
        ShellTests.capsFileShape ()
        ShellTests.capsContextFormat ()
        ShellTests.capsFileSizeLimit ()
        ShellTests.ollamaFormat ()
        ShellTests.summarizerInputCap ()
        DynTests.nullish ()
        DelegateTests.run ()
        ResolveAiSettingsTests.run ()
        do! IntegrationPluginTests.run () |> Async.AwaitPromise
        do! IntegrationEventTests.run () |> Async.AwaitPromise
        do! IntegrationDedupTests.run () |> Async.AwaitPromise
        do! IntegrationToolTests.run () |> Async.AwaitPromise
        do! IntegrationChatTests.run () |> Async.AwaitPromise
        MagicTests.run ()
        return summary ()
    }
    |> Async.StartAsPromise
