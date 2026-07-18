module Wanxiangshu.Tests.TestsEntriesOmp

open Wanxiangshu.Tests.TestsTestBody
open Wanxiangshu.Tests.OmpKernelTests
open Wanxiangshu.Tests.OmpSessionToolsTests
open Wanxiangshu.Tests.OmpWebFetchTests
open Wanxiangshu.Tests.OmpCapsTests
open Wanxiangshu.Tests.OmpFuzzyTests
open Wanxiangshu.Tests.OmpPluginTests
open Wanxiangshu.Tests.OmpContextTransformTests
open Wanxiangshu.Tests.TestsEntriesOmp2

let private entries1 () : (string * TestBody) list =
    [ "OmpKernelTests.filterOmpMainSessionTools", Sync(sync OmpKernelTests.filterOmpMainSessionTools)
      "OmpKernelTests.validateFetchUrlBlocksPrivate", Sync(sync OmpKernelTests.validateFetchUrlBlocksPrivate)
      "OmpKernelTests.reviewInstructionsCanonicalVerdictTool",
      Sync(sync OmpKernelTests.reviewInstructionsCanonicalVerdictTool)
      "OmpSessionToolsTests.mainSessionStripsChildOnlyAndBash",
      Sync(sync OmpSessionToolsTests.mainSessionStripsChildOnlyAndBash)
      "OmpSessionToolsTests.childSessionKeepsChildTools", Sync(sync OmpSessionToolsTests.childSessionKeepsChildTools)
      "OmpWebFetchTests.blocksLocalhostAndPrivateRanges", Sync(sync OmpWebFetchTests.blocksLocalhostAndPrivateRanges)
      "OmpWebFetchTests.rejectsUnsupportedScheme", Sync(sync OmpWebFetchTests.rejectsUnsupportedScheme)
      "OmpCapsTests.buildCapsFromUppercaseFiles", Async OmpCapsTests.buildCapsFromUppercaseFiles
      "OmpCapsTests.stripHostDirContext", Sync(sync OmpCapsTests.stripHostDirContext)
      "OmpCapsTests.appendCapsIdempotent", Async OmpCapsTests.appendCapsIdempotent
      "OmpCapsTests.capsSkipsExcludedDirs", Async OmpCapsTests.capsSkipsExcludedDirs
      "OmpCapsTests.capsRespectsFileCountBudget", Async OmpCapsTests.capsRespectsFileCountBudget
      "OmpFuzzyTests.fuzzyFindIteratorSingleUse", Sync(sync OmpFuzzyTests.fuzzyFindIteratorSingleUse)
      "OmpFuzzyTests.fuzzyGrepIteratorSingleUse", Sync(sync OmpFuzzyTests.fuzzyGrepIteratorSingleUse)
      "OmpFuzzyTests.registeredFuzzyToolsExposeIteratorParam",
      Async OmpFuzzyTests.registeredFuzzyToolsExposeIteratorParam
      "OmpKernelTests.executorSummarizerPromptCarriesWhatToSummarize",
      Sync(sync OmpKernelTests.executorSummarizerPromptCarriesWhatToSummarize)
      "OmpPluginTests.registersCoreToolsIdempotent", Async OmpPluginTests.registersCoreToolsIdempotent
      "OmpPluginTests.meditatorSchemaUnifiedNote", Async OmpPluginTests.meditatorSchemaUnifiedNote
      "OmpPluginTests.sessionStartStripsMainSessionTools", Async OmpPluginTests.sessionStartStripsMainSessionTools
      "OmpPluginTests.fuzzyDescriptionsMatchMuxWording", Sync(sync OmpPluginTests.fuzzyDescriptionsMatchMuxWording)
      "OmpContextTransformTests.capsSynthUserPrepended", Async OmpContextTransformTests.capsSynthUserPrepended
      "OmpContextTransformTests.capsReadToolsInContextTransform",
      Async OmpContextTransformTests.capsReadToolsInContextTransform
      "OmpContextTransformTests.testInvestigatorCrashWithUndefinedCaps",
      Async OmpContextTransformTests.testInvestigatorCrashWithUndefinedCaps
      "OmpContextTransformTests.beforeAgentStartOmitsCapsXml",
      Async OmpContextTransformTests.beforeAgentStartOmitsCapsXml
      "OmpContextTransformTests.reviewReplayIfStoreEmptyOnTransform",
      Async OmpContextTransformTests.reviewReplayIfStoreEmptyOnTransform
      "OmpPluginTests.readAssistantTextFromEntries", Sync(sync OmpPluginTests.readAssistantTextFromEntries)
      "OmpPluginTests.subagentPromptsContainKernelFragments",
      Sync(sync OmpPluginTests.subagentPromptsContainKernelFragments)
      "OmpPluginTests.executorToolSchemaFourFields", Async OmpPluginTests.executorToolSchemaFourFields
      "OmpPluginTests.browserErrorsWithoutBrowserHost", Async OmpPluginTests.browserErrorsWithoutBrowserHost
      "OmpPluginTests.reviewChildInitialPromptUsesReturnReviewer",
      Sync(sync OmpPluginTests.reviewChildInitialPromptUsesReturnReviewer)
      "OmpPluginTests.fuzzyGrepExcludeAnyOfLength2", Async OmpPluginTests.fuzzyGrepExcludeAnyOfLength2 ]

let ompTestEntries () : (string * TestBody) list =
    entries1 ()
    @ entries2 ()
    @ TestsEntriesOmp2.entries3 ()
    @ TestsEntriesOmp2.entries4 ()
