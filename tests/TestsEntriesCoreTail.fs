module Wanxiangshu.Tests.TestsEntriesCoreTail

open Wanxiangshu.Tests.FuzzyTests
open Wanxiangshu.Tests.FuzzyTestsPromise
open Wanxiangshu.Tests.FuzzyTestsPaging
open Wanxiangshu.Tests.ShellTests
open Wanxiangshu.Tests.ShellTestsFormat
open Wanxiangshu.Tests.DynTests
open Wanxiangshu.Tests.DelegateTests
open Wanxiangshu.Tests.DelegateToolsCodecTests
open Wanxiangshu.Tests.PatchParserTests
open Wanxiangshu.Tests.ResolveAiSettingsTests
open Wanxiangshu.Tests.IntegrationPluginTests
open Wanxiangshu.Tests.IntegrationEventTests
open Wanxiangshu.Tests.IntegrationOpencodeReviewSpecs
open Wanxiangshu.Tests.IntegrationChatTests
open Wanxiangshu.Tests.IntegrationChatTestsSubagent
open Wanxiangshu.Tests.OpencodeSessionLifecycleTests
open Wanxiangshu.Tests.LoopMessagesTests
open Wanxiangshu.Tests.MessagingTests
open Wanxiangshu.Tests.WorkBacklogTests
open Wanxiangshu.Tests.MethodologyTests
open Wanxiangshu.Tests.MethodologyRegistryTests
open Wanxiangshu.Tests.ToolCatalogRegistryTests
open Wanxiangshu.Tests.TitleFetchGuardTests
open Wanxiangshu.Tests.ToolCatalogClassificationTests
open Wanxiangshu.Tests.ToolOutputInfoTests
open Wanxiangshu.Tests.MessageTransformPolicyTests
open Wanxiangshu.Tests.MessageTransformStackTests
open Wanxiangshu.Tests.ParallelToolPromptTests
open Wanxiangshu.Tests.CompactionTransformTests
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
open Wanxiangshu.Tests.WarnTddKernelFactsTests
open Wanxiangshu.Tests.WarnTddOpencodeEnforcementTests
open Wanxiangshu.Tests.WarnTddMuxEnforcementTests
open Wanxiangshu.Tests.WarnTddOmpEnforcementTests
open Wanxiangshu.Tests.EventDrivenHarnessDemo
open Wanxiangshu.Tests.SubagentOutputTranscriptTests
open Wanxiangshu.Tests.SubagentDrainingTests
open Wanxiangshu.Tests.TestsTestBody

let tailCoreTestEntriesPart1 () : (string * TestBody) list =
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
      "FuzzyTestsPaging.emptyIteratorTreatedAsAbsent", TestBody.Sync(sync FuzzyTestsPaging.emptyIteratorTreatedAsAbsent)
      "ShellTests.webApiFetchInit", TestBody.Sync(sync ShellTests.webApiFetchInit)
      "ShellTests.webApiResponseMethodCall", TestBody.Sync(sync ShellTests.webApiResponseMethodCall)
      "ShellTests.webApiKeyValidation", TestBody.Sync(sync ShellTests.webApiKeyValidation)
      "ShellTests.executorMapping", TestBody.Sync(sync ShellTests.executorMapping)
      "ShellTestsFormat.safetyWarning", TestBody.Sync(sync ShellTestsFormat.safetyWarning)
      "ShellTests.capsFileShape", TestBody.Sync(sync ShellTests.capsFileShape)
      "ShellTests.capsFileSizeLimit", TestBody.Sync(sync ShellTests.capsFileSizeLimit)
      "ShellTests.stripHeadTailPipesOutsideQuotes", TestBody.Sync(sync ShellTests.stripHeadTailPipesOutsideQuotes)
      "ShellTests.stripHeadTailPipesHeadTailChain", TestBody.Sync(sync ShellTests.stripHeadTailPipesHeadTailChain)
      "ShellTestsFormat.ollamaFormat", TestBody.Sync(sync ShellTestsFormat.ollamaFormat)
      "ShellTestsFormat.webApiSearchFormat", TestBody.Sync(sync ShellTestsFormat.webApiSearchFormat)
      "ShellTestsFormat.summarizerInputCap", TestBody.Sync(sync ShellTestsFormat.summarizerInputCap)
      "ShellTestsFormat.executorToolResponseFormatting",
      TestBody.Sync(sync ShellTestsFormat.executorToolResponseFormatting)
      "ShellTestsFormat.summarizerPromptOmitsReturnValue",
      TestBody.Sync(sync ShellTestsFormat.summarizerPromptOmitsReturnValue)
      "ShellTestsFormat.formatFetchResponseAllFields", TestBody.Sync(sync ShellTestsFormat.formatFetchResponseAllFields)
      "ShellTestsFormat.formatFetchResponseOnlyTitle", TestBody.Sync(sync ShellTestsFormat.formatFetchResponseOnlyTitle)
      "ShellTestsFormat.formatFetchResponseOnlyContent",
      TestBody.Sync(sync ShellTestsFormat.formatFetchResponseOnlyContent)
      "ShellTestsFormat.formatFetchResponseAllNone", TestBody.Sync(sync ShellTestsFormat.formatFetchResponseAllNone)
      "ShellTestsFormat.formatFetchResponseEmptyTitleOmitted",
      TestBody.Sync(sync ShellTestsFormat.formatFetchResponseEmptyTitleOmitted) ]

let tailCoreTestEntriesPart2 () : (string * TestBody) list =
    [ "ShellTests.readDirectoryListing", TestBody.Async ShellTests.readDirectoryListing
      "ShellTests.ensureJavascriptProjectRepairsModuleType",
      TestBody.Async ShellTests.ensureJavascriptProjectRepairsModuleType
      "ShellTests.rewriteJavascriptRelativeImports", TestBody.Async ShellTests.rewriteJavascriptRelativeImports
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
      "WorkBacklogTests.run", TestBody.Sync(sync WorkBacklogTests.run)
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

let tailCoreTestEntriesPart3 () : (string * TestBody) list =
    [ "ToolCatalogClassificationTests.run", TestBody.Sync(sync ToolCatalogClassificationTests.run)
      "ToolOutputInfoTests.run", TestBody.Sync(sync ToolOutputInfoTests.run)
      "MessageTransformPolicyTests.run", TestBody.Async MessageTransformPolicyTests.run
      "MessageTransformStackTests.run", TestBody.Async MessageTransformStackTests.run
      "ParallelToolPromptTests.run", TestBody.Async ParallelToolPromptTests.run
      "CompactionTransformTests.run", TestBody.Async CompactionTransformTests.run
      "MessageSanitizationTests.run", TestBody.Async MessageSanitizationTests.run
      "SembleInjectionTests.run", TestBody.Sync(sync SembleInjectionTests.run)
      "SembleReviewerInjectionTests.testSembleInjectsForReviewer", TestBody.Async testSembleInjectsForReviewer
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
        "WarnTddKernelFactsTests.run", TestBody.Sync(sync WarnTddKernelFactsTests.run)
        "WarnTddOpencodeEnforcementTests.run", TestBody.Async WarnTddOpencodeEnforcementTests.run
        "WarnTddMuxEnforcementTests.run", TestBody.Async WarnTddMuxEnforcementTests.run
        "WarnTddOmpEnforcementTests.run", TestBody.Sync(sync WarnTddOmpEnforcementTests.run)
        "EventDrivenHarnessDemo.run", TestBody.Async(fun () -> EventDrivenHarnessDemo.run ())
        "ProductionDebugOutputTests.run", TestBody.Sync(sync Wanxiangshu.Tests.ProductionDebugOutputTests.run)
        "ArchitectureGatesTests.run", TestBody.Sync(sync Wanxiangshu.Tests.ArchitectureGatesTests.run) ]

let tailCoreTestEntries () : (string * TestBody) list =
    tailCoreTestEntriesPart1 ()
    @ tailCoreTestEntriesPart2 ()
    @ tailCoreTestEntriesPart3 ()
