module Wanxiangshu.Tests.TestsEntriesOmp

open Wanxiangshu.Tests.TestsTestBody
open Wanxiangshu.Tests.OmpKernelTests
open Wanxiangshu.Tests.OmpSessionToolsTests
open Wanxiangshu.Tests.OmpCapsTests
open Wanxiangshu.Tests.OmpPluginTests
open Wanxiangshu.Tests.OmpContextTransformTests
open Wanxiangshu.Tests.TestsEntriesOmp2

let private entries1 () : (string * TestBody) list =
    [ "OmpKernelTests.filterOmpMainSessionTools", Sync(sync OmpKernelTests.filterOmpMainSessionTools)
      "OmpKernelTests.reviewInstructionsCanonicalVerdictTool",
      Sync(sync OmpKernelTests.reviewInstructionsCanonicalVerdictTool)
      "OmpSessionToolsTests.mainSessionStripsChildOnlyAndBash",
      Sync(sync OmpSessionToolsTests.mainSessionStripsChildOnlyAndBash)
      "OmpSessionToolsTests.childSessionKeepsChildTools", Sync(sync OmpSessionToolsTests.childSessionKeepsChildTools)
      "OmpCapsTests.buildCapsFromUppercaseFiles", Async OmpCapsTests.buildCapsFromUppercaseFiles
      "OmpCapsTests.stripHostDirContext", Sync(sync OmpCapsTests.stripHostDirContext)
      "OmpCapsTests.appendCapsIdempotent", Async OmpCapsTests.appendCapsIdempotent
      "OmpCapsTests.capsSkipsExcludedDirs", Async OmpCapsTests.capsSkipsExcludedDirs
      "OmpCapsTests.capsRespectsFileCountBudget", Async OmpCapsTests.capsRespectsFileCountBudget
      "OmpKernelTests.executorSummarizerPromptCarriesWhatToSummarize",
      Sync(sync OmpKernelTests.executorSummarizerPromptCarriesWhatToSummarize)
      "OmpPluginTests.registersCoreToolsIdempotent", Async OmpPluginTests.registersCoreToolsIdempotent
      "OmpPluginTests.meditatorSchemaUnifiedNote", Async OmpPluginTests.meditatorSchemaUnifiedNote
      "OmpPluginTests.sessionStartStripsMainSessionTools", Async OmpPluginTests.sessionStartStripsMainSessionTools
      "OmpContextTransformTests.capsSynthUserPrepended", Async OmpContextTransformTests.capsSynthUserPrepended
      "OmpContextTransformTests.capsReadToolsInContextTransform",
      Async OmpContextTransformTests.capsReadToolsInContextTransform
      "OmpContextTransformTests.testInspectorCrashWithUndefinedCaps",
      Async OmpContextTransformTests.testInspectorCrashWithUndefinedCaps
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
      Sync(sync OmpPluginTests.reviewChildInitialPromptUsesReturnReviewer) ]

let ompTestEntries () : (string * TestBody) list =
    entries1 ()
    @ entries2 ()
    @ TestsEntriesOmp2.entries3 ()
    @ TestsEntriesOmp2.entries4 ()
