module Wanxiangshu.Tests.KernelAndSupportCoverageEntries

open Wanxiangshu.Tests.TestsTestBody
open Wanxiangshu.Tests.SearchToolsTests
open Wanxiangshu.Tests.KernelDomainIdentityTests
open Wanxiangshu.Tests.KernelReviewSessionCoverageTests
open Wanxiangshu.Tests.MethodologyArgsCoverageTests
open Wanxiangshu.Tests.OpencodeToolSchemaCoverageTests
open Wanxiangshu.Tests.KernelDomainCoverageTests
open Wanxiangshu.Tests.KernelDomainCoverageTestsToolArgs
open Wanxiangshu.Tests.KernelReviewCoverageTests
open Wanxiangshu.Tests.KernelCoverageTestsMethodology
open Wanxiangshu.Tests.OmpToolsCoverageTests
open Wanxiangshu.Tests.OmpCodecCoverageTests
open Wanxiangshu.Tests.PluginMimoTuiTests
open Wanxiangshu.Tests.LivelockGuardScopeTests
open Wanxiangshu.Tests.OpencodeHookSchemaCoverageTests
open Wanxiangshu.Tests.OpencodeSearchToolsCoverageTests
open Wanxiangshu.Tests.OpencodeSubagentCoverageTests
open Wanxiangshu.Tests.ReviewerLoopTests
open Wanxiangshu.Tests.ReviewerLoopCleanupTests
open Wanxiangshu.Tests.SubagentPromptAbortTests
open Wanxiangshu.Tests.MuxCoverageTests

let kernelSupportCoverageTestEntries () : (string * TestBody) list =
    [ "KernelDomainIdentityTests.run", Sync(sync KernelDomainIdentityTests.run)
      "KernelReviewSessionCoverageTests.run", Sync(sync KernelReviewSessionCoverageTests.run)
      "MethodologyArgsCoverageTests.run", Sync(sync MethodologyArgsCoverageTests.run)
      "OpencodeToolSchemaCoverageTests.run", Sync(sync OpencodeToolSchemaCoverageTests.run)
      "KernelDomainCoverageTests.run", Sync(sync KernelDomainCoverageTests.run)
      "KernelDomainCoverageTestsToolArgs.run", Sync(sync KernelDomainCoverageTestsToolArgs.run)
      "KernelReviewCoverageTests.run", Sync(sync KernelReviewCoverageTests.run)
      "KernelCoverageTestsMethodology.run", Sync(sync KernelCoverageTestsMethodology.run)
      "OmpToolsCoverageTests.run", Async OmpToolsCoverageTests.run
      "OmpCodecCoverageTests.run", Async OmpCodecCoverageTests.run
      "SearchToolsTests.run", Async SearchToolsTests.run
      "PluginMimoTuiTests.run", Sync(sync PluginMimoTuiTests.run)
      "MuxCoverageTests.run", Sync(sync MuxCoverageTests.run)
      "OpencodeHookSchemaCoverageTests.run", Async OpencodeHookSchemaCoverageTests.run
      "OpencodeSearchToolsCoverageTests.run", Async OpencodeSearchToolsCoverageTests.run
      "OpencodeSubagentCoverageTests.run", Async OpencodeSubagentCoverageTests.run
      "ReviewerLoopTests.run", Async ReviewerLoopTests.run
      "ReviewerLoopCleanupTests.run", Async ReviewerLoopCleanupTests.run
      "SubagentPromptAbortTests.run", Async SubagentPromptAbortTests.run
      "LivelockGuardScopeTests.run", Sync(sync LivelockGuardScopeTests.run) ]
