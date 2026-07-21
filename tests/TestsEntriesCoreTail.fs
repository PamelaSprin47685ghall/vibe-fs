module Wanxiangshu.Tests.TestsEntriesCoreTail

open Wanxiangshu.Tests.FuzzyTests
open Wanxiangshu.Tests.FuzzyTestsPromise
open Wanxiangshu.Tests.FuzzyTestsPaging
open Wanxiangshu.Tests.ExecutorSpawnPathTests
open Wanxiangshu.Tests.ExecutorFormatCoverageTests
open Wanxiangshu.Tests.DynTests
open Wanxiangshu.Tests.DelegateTests
open Wanxiangshu.Tests.DelegateToolsCodecTests
open Wanxiangshu.Tests.PatchParserTests
open Wanxiangshu.Tests.ResolveAiSettingsTests
open Wanxiangshu.Tests.ProductionDebugOutputTests
open Wanxiangshu.Tests.PendingEvidenceEpochTests
open Wanxiangshu.Tests.IntegrationPluginTests
open Wanxiangshu.Tests.IntegrationEventTests
open Wanxiangshu.Tests.IntegrationOpencodeReviewSpecs
open Wanxiangshu.Tests.IntegrationChatTests
open Wanxiangshu.Tests.IntegrationChatTestsSubagent
open Wanxiangshu.Tests.OpencodeSessionLifecycleTests
open Wanxiangshu.Tests.LoopMessagesTests
open Wanxiangshu.Tests.MessagingTests
open Wanxiangshu.Tests.MethodologyTests
open Wanxiangshu.Tests.MethodologyRegistryTests
open Wanxiangshu.Tests.ToolCatalogRegistryTests
open Wanxiangshu.Tests.TitleFetchGuardTests
open Wanxiangshu.Tests.ToolCatalogClassificationTests
open Wanxiangshu.Tests.ToolOutputInfoTests
open Wanxiangshu.Tests.MessageTransformPolicyTests
open Wanxiangshu.Tests.MessageTransformStackTests
open Wanxiangshu.Tests.ParallelToolPromptTests
open Wanxiangshu.Tests.ModelResolutionTests
open Wanxiangshu.Tests.MessageSanitizationTests
open Wanxiangshu.Tests.SembleInjectionTests
open Wanxiangshu.Tests.SembleReviewerInjectionTests
open Wanxiangshu.Tests.ExecutorKernelTests
open Wanxiangshu.Tests.ToolExecuteTests
open Wanxiangshu.Tests.TreeSitterKernelTests
open Wanxiangshu.Tests.ConfigTests
open Wanxiangshu.Tests.JsonSchemaBuildersTests
open Wanxiangshu.Tests.ExecutorStripTests
open Wanxiangshu.Tests.ExecutorTests

open Wanxiangshu.Tests.EventDrivenHarnessDemo
open Wanxiangshu.Tests.SubagentOutputTranscriptTests
open Wanxiangshu.Tests.SubagentDrainingTests
open Wanxiangshu.Tests.TestsTestBody
open Wanxiangshu.Tests.PluginObjectContractTests
open Wanxiangshu.Tests.MuxCapabilityContractTests
open Wanxiangshu.Tests.RuntimeScopeLifecycleTests

let tailCoreTestEntriesFuzzy () : (string * TestBody) list =
    [ "FuzzyTests.grepDetect", TestBody.Sync(sync FuzzyTests.grepDetect)
      "FuzzyTests.iteratorRoundTrip", TestBody.Sync(sync FuzzyTests.iteratorRoundTrip)
      "FuzzyTests.finderConversion", TestBody.Sync(sync FuzzyTests.finderConversion)
      "FuzzyTests.formatFull", TestBody.Sync(sync FuzzyTests.formatFull)
      "FuzzyTests.fuzzyFallbackNotice", TestBody.Sync(sync FuzzyTests.fuzzyFallbackNotice)
      "FuzzyTests.scanTimeoutConfigurable", TestBody.Sync(sync FuzzyTests.scanTimeoutConfigurable)
      "FuzzyTests.iteratorCounterUniqueness", TestBody.Sync(sync FuzzyTests.iteratorCounterUniqueness)
      "FuzzyTestsPromise.finderCacheConcurrencyRace", TestBody.Async FuzzyTestsPromise.finderCacheConcurrencyRace
      "FuzzyTestsPromise.grepMaxMatchesPerFileRespectsPageSize",
      TestBody.Async FuzzyTestsPromise.grepMaxMatchesPerFileRespectsPageSize
      "FuzzyTestsPromise.findPagingWhenTotalMatchedIsNone",
      TestBody.Async FuzzyTestsPromise.findPagingWhenTotalMatchedIsNone
      "FuzzyTestsPromise.grepMultiPropagatesErrorAndSafety",
      TestBody.Async FuzzyTestsPromise.grepMultiPropagatesErrorAndSafety
      "FuzzyTestsPaging.findPagingDefault", TestBody.Sync(sync FuzzyTestsPaging.findPagingDefault)
      "FuzzyTestsPaging.emptyIteratorNotRendered", TestBody.Sync(sync FuzzyTestsPaging.emptyIteratorNotRendered)
      "FuzzyTestsPaging.totalMatchedSemantics", TestBody.Sync(sync FuzzyTestsPaging.totalMatchedSemantics)
      "FuzzyTestsPaging.grepOutputNotices", TestBody.Sync(sync FuzzyTestsPaging.grepOutputNotices)
      "FuzzyTestsPaging.iteratorNamespaceConstants", TestBody.Sync(sync FuzzyTestsPaging.iteratorNamespaceConstants)
      "FuzzyTestsPaging.iteratorStoreStronglyTyped", TestBody.Sync(sync FuzzyTestsPaging.iteratorStoreStronglyTyped)
      "FuzzyTestsPaging.runWithFinderSharedPipeline", TestBody.Sync(sync FuzzyTestsPaging.runWithFinderSharedPipeline)
      "FuzzyTestsPaging.resolveStoreRequiresInjection",
      TestBody.Sync(sync FuzzyTestsPaging.resolveStoreRequiresInjection)
      "FuzzyTestsPaging.emptyIteratorTreatedAsAbsent", TestBody.Sync(sync FuzzyTestsPaging.emptyIteratorTreatedAsAbsent) ]

let tailCoreTestEntriesExecutor () : (string * TestBody) list =
    [ "ExecutorSpawnPathTests.webApiFetchInit", TestBody.Sync(sync ExecutorSpawnPathTests.webApiFetchInit)
      "ExecutorSpawnPathTests.webApiResponseMethodCall",
      TestBody.Sync(sync ExecutorSpawnPathTests.webApiResponseMethodCall)
      "ExecutorSpawnPathTests.webApiKeyValidation", TestBody.Sync(sync ExecutorSpawnPathTests.webApiKeyValidation)
      "ExecutorSpawnPathTests.executorMapping", TestBody.Sync(sync ExecutorSpawnPathTests.executorMapping)
      "ExecutorFormatCoverageTests.safetyWarning", TestBody.Sync(sync ExecutorFormatCoverageTests.safetyWarning)
      "ExecutorSpawnPathTests.capsFileShape", TestBody.Sync(sync ExecutorSpawnPathTests.capsFileShape)
      "ExecutorSpawnPathTests.capsFileSizeLimit", TestBody.Sync(sync ExecutorSpawnPathTests.capsFileSizeLimit)
      "ExecutorSpawnPathTests.stripHeadTailPipesOutsideQuotes",
      TestBody.Sync(sync ExecutorSpawnPathTests.stripHeadTailPipesOutsideQuotes)
      "ExecutorSpawnPathTests.stripHeadTailPipesHeadTailChain",
      TestBody.Sync(sync ExecutorSpawnPathTests.stripHeadTailPipesHeadTailChain)
      "ExecutorFormatCoverageTests.ollamaFormat", TestBody.Sync(sync ExecutorFormatCoverageTests.ollamaFormat)
      "ExecutorFormatCoverageTests.webApiSearchFormat",
      TestBody.Sync(sync ExecutorFormatCoverageTests.webApiSearchFormat)
      "ExecutorFormatCoverageTests.summarizerInputCap",
      TestBody.Sync(sync ExecutorFormatCoverageTests.summarizerInputCap)
      "ExecutorFormatCoverageTests.executorToolResponseFormatting",
      TestBody.Sync(sync ExecutorFormatCoverageTests.executorToolResponseFormatting)
      "ExecutorFormatCoverageTests.summarizerPromptOmitsReturnValue",
      TestBody.Sync(sync ExecutorFormatCoverageTests.summarizerPromptOmitsReturnValue)
      "ExecutorFormatCoverageTests.formatFetchResponseAllFields",
      TestBody.Sync(sync ExecutorFormatCoverageTests.formatFetchResponseAllFields)
      "ExecutorFormatCoverageTests.formatFetchResponseOnlyTitle",
      TestBody.Sync(sync ExecutorFormatCoverageTests.formatFetchResponseOnlyTitle)
      "ExecutorFormatCoverageTests.formatFetchResponseOnlyContent",
      TestBody.Sync(sync ExecutorFormatCoverageTests.formatFetchResponseOnlyContent)
      "ExecutorFormatCoverageTests.formatFetchResponseAllNone",
      TestBody.Sync(sync ExecutorFormatCoverageTests.formatFetchResponseAllNone)
      "ExecutorFormatCoverageTests.formatFetchResponseEmptyTitleOmitted",
      TestBody.Sync(sync ExecutorFormatCoverageTests.formatFetchResponseEmptyTitleOmitted) ]

let tailCoreTestEntriesGroup2 () : (string * TestBody) list =
    [ "ExecutorSpawnPathTests.readDirectoryListing", TestBody.Async ExecutorSpawnPathTests.readDirectoryListing
      "ExecutorSpawnPathTests.ensureJavascriptProjectRepairsModuleType",
      TestBody.Async ExecutorSpawnPathTests.ensureJavascriptProjectRepairsModuleType
      "ExecutorSpawnPathTests.rewriteJavascriptRelativeImports",
      TestBody.Async ExecutorSpawnPathTests.rewriteJavascriptRelativeImports
      "DynTests.nullish", TestBody.Sync(sync DynTests.nullish)
      "DelegateTests.run", TestBody.Sync(sync DelegateTests.run)
      "DelegateToolsCodecTests.run", TestBody.Sync(sync DelegateToolsCodecTests.run)
      "PatchParserTests.run", TestBody.Sync(sync PatchParserTests.run)
      "ResolveAiSettingsTests.run", TestBody.Sync(sync ResolveAiSettingsTests.run)
      "IntegrationPluginTests.run", TestBody.Async IntegrationPluginTests.run
      "IntegrationEventTests.run", TestBody.Async IntegrationEventTests.run
      "IntegrationOpencodeReviewSpecs.run", TestBody.Async IntegrationOpencodeReviewSpecs.run
      "IntegrationChatTests.run", TestBody.Async IntegrationChatTests.run
      "IntegrationChatTestsSubagent.run", TestBody.Async IntegrationChatTestsSubagent.run
      "OpencodeSessionLifecycleTests.childIdleDoesNotAbortParent",
      TestBody.Async OpencodeSessionLifecycleTests.childIdleDoesNotAbortParent
      "LoopMessagesTests.run", TestBody.Sync(sync LoopMessagesTests.run)
      "MessagingTests.run", TestBody.Sync(sync MessagingTests.run)
      "MethodologyTests.run", TestBody.Sync(sync MethodologyTests.run)
      "MethodologyRegistryTests.run", TestBody.Sync(sync MethodologyRegistryTests.run)
      "ToolCatalogRegistryTests.run", TestBody.Sync(sync ToolCatalogRegistryTests.run)
      "TitleFetchGuardTests.signature", TestBody.Sync(sync TitleFetchGuardTests.signature)
      "TitleFetchGuardTests.wrap", TestBody.Sync(sync TitleFetchGuardTests.wrap)
      "TitleFetchGuardTests.detect", TestBody.Sync(sync TitleFetchGuardTests.detect)
      "TitleFetchGuardTests.tryWrapString", TestBody.Sync(sync TitleFetchGuardTests.tryWrapString)
      "TitleFetchGuardTests.rewriteStringContent", TestBody.Sync(sync TitleFetchGuardTests.rewriteStringContent)
      "TitleFetchGuardTests.rewriteArrayContent", TestBody.Sync(sync TitleFetchGuardTests.rewriteArrayContent)
      "TitleFetchGuardTests.skipProbeMessage", TestBody.Sync(sync TitleFetchGuardTests.skipProbeMessage) ]

let tailCoreTestEntriesGroup3 () : (string * TestBody) list =
    [ "ToolCatalogClassificationTests.run", TestBody.Sync(sync ToolCatalogClassificationTests.run)
      "ToolOutputInfoTests.run", TestBody.Sync(sync ToolOutputInfoTests.run)
      "MessageTransformPolicyTests.run", TestBody.Async MessageTransformPolicyTests.run
      "MessageTransformStackTests.run", TestBody.Async MessageTransformStackTests.run
      "ParallelToolPromptTests.run", TestBody.Async ParallelToolPromptTests.run
      "ModelResolutionTests.run", TestBody.Async ModelResolutionTests.run
      "MessageSanitizationTests.run", TestBody.Async MessageSanitizationTests.run
      "SembleInjectionTests.run", TestBody.Sync(sync SembleInjectionTests.run)
      "SembleReviewerInjectionTests.testSembleCoderBlocked", TestBody.Async testSembleCoderBlocked
      "SembleReviewerInjectionTests.testSembleReviewerAllows", TestBody.Async testSembleReviewerAllows
      "SembleReviewerInjectionTests.testAmendSkippedWhenSembleInjectEnabled",
      TestBody.Async testAmendSkippedWhenSembleInjectEnabled
      "ExecutorKernelTests.run", TestBody.Sync(sync ExecutorKernelTests.run)
      "ToolExecuteTests.run", TestBody.Sync(sync ToolExecuteTests.run)
      "TreeSitterKernelTests.run", TestBody.Sync(sync TreeSitterKernelTests.run)
      "SubagentOutputTranscriptTests.run", TestBody.Sync(sync SubagentOutputTranscriptTests.run)
      "SubagentDrainingTests.run", TestBody.Sync(sync SubagentDrainingTests.run) ]
    @ (TreeSitterKernelTests.generateSelfCheckBodies ()
       |> List.map (fun (l, f) -> l, TestBody.Async f))
    @ [ "ConfigTests.run", TestBody.Sync(sync ConfigTests.run)
        "JsonSchemaBuildersTests.run", TestBody.Sync(sync JsonSchemaBuildersTests.run)
        "ExecutorStripTests.run", TestBody.Sync(sync ExecutorStripTests.run)
        "ExecutorTests.infiniteStdoutBounded", TestBody.Async ExecutorTests.infiniteStdoutBounded
        "ExecutorTests.infiniteStderrBounded", TestBody.Async ExecutorTests.infiniteStderrBounded
        "ExecutorTests.smallOutputUnchanged", TestBody.Async ExecutorTests.smallOutputUnchanged

        "EventDrivenHarnessDemo.run", TestBody.Async(fun () -> EventDrivenHarnessDemo.run ())
        "ProductionDebugOutputTests.run", TestBody.Sync(sync Wanxiangshu.Tests.ProductionDebugOutputTests.run)
        "RemovedProductionFilesTests.run", TestBody.Sync(sync Wanxiangshu.Tests.RemovedProductionFilesTests.run)
        "ForbiddenSourceSymbolsTests.run", TestBody.Sync(sync Wanxiangshu.Tests.ForbiddenSourceSymbolsTests.run)
        "PluginObjectContractTests.run", TestBody.Sync(sync PluginObjectContractTests.run)
        "MuxCapabilityContractTests.run", TestBody.Sync(sync MuxCapabilityContractTests.run)
        "ProfilerDefaultTests.run", TestBody.Sync(sync Wanxiangshu.Tests.ProfilerDefaultTests.run)
        "ProfilerOutputTests.run", TestBody.Sync(sync Wanxiangshu.Tests.ProfilerOutputTests.run)
        "PendingEvidenceEpochTests.run", TestBody.Sync(sync Wanxiangshu.Tests.PendingEvidenceEpochTests.run)
        "ContinuationCleanupTests.run", TestBody.Sync(sync Wanxiangshu.Tests.ContinuationCleanupTests.run)
        "RuntimeScopeLifecycleTests.run", TestBody.Async Wanxiangshu.Tests.RuntimeScopeLifecycleTests.run ]

let tailCoreTestEntries () : (string * TestBody) list =
    tailCoreTestEntriesFuzzy ()
    @ tailCoreTestEntriesExecutor ()
    @ tailCoreTestEntriesGroup2 ()
    @ tailCoreTestEntriesGroup3 ()
