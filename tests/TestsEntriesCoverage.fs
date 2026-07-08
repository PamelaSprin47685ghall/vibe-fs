module Wanxiangshu.Tests.TestsEntriesCoverage

open Wanxiangshu.Tests.TestsTestBody
open Wanxiangshu.Tests.SearchToolsTests
open Wanxiangshu.Tests.CoverageFillKernelTests
open Wanxiangshu.Tests.CoverageFillKernel2Tests
open Wanxiangshu.Tests.CoverageFillShellTests
open Wanxiangshu.Tests.CoverageFillFuzzyTests
open Wanxiangshu.Tests.CoverageFillMethodologyTests
open Wanxiangshu.Tests.FuzzySearchGrepTests
open Wanxiangshu.Tests.CoverageFillOpencodeTests
open Wanxiangshu.Tests.KernelDomainCoverageTests
open Wanxiangshu.Tests.KernelReviewCoverageTests
open Wanxiangshu.Tests.OmpToolsCoverageTests
open Wanxiangshu.Tests.OmpCodecCoverageTests
open Wanxiangshu.Tests.PluginMimoTuiTests
open Wanxiangshu.Tests.ShellCoverage2Tests
open Wanxiangshu.Tests.OpencodeHookSchemaCoverageTests
open Wanxiangshu.Tests.OpencodeSearchToolsCoverageTests
open Wanxiangshu.Tests.OpencodeSubagentCoverageTests
open Wanxiangshu.Tests.ReviewerLoopTests

let coverageTestEntries () : (string * TestBody) list =
    [ "CoverageFillKernelTests.run", Sync(sync CoverageFillKernelTests.run)
      "CoverageFillKernel2Tests.run", Sync(sync CoverageFillKernel2Tests.run)
      "CoverageFillShellTests.run", Async CoverageFillShellTests.run
      "CoverageFillFuzzyTests.run", Sync(sync CoverageFillFuzzyTests.run)
      "FuzzySearchGrepTests.run", Sync(sync FuzzySearchGrepTests.run)
      "CoverageFillMethodologyTests.run", Sync(sync CoverageFillMethodologyTests.run)
      "CoverageFillOpencodeTests.run", Sync(sync CoverageFillOpencodeTests.run)
      "KernelDomainCoverageTests.run", Sync(sync KernelDomainCoverageTests.run)
      "KernelReviewCoverageTests.run", Sync(sync KernelReviewCoverageTests.run)
      "OmpToolsCoverageTests.run", Async OmpToolsCoverageTests.run
      "OmpCodecCoverageTests.run", Async OmpCodecCoverageTests.run
      "SearchToolsTests.run", Async SearchToolsTests.run
      "PluginMimoTuiTests.run", Sync(sync PluginMimoTuiTests.run)
      "MuxCoverageTests.run", Sync(sync MuxCoverageTests.run)
      "OpencodeHookSchemaCoverageTests.run", Async OpencodeHookSchemaCoverageTests.run
      "OpencodeSearchToolsCoverageTests.run", Async OpencodeSearchToolsCoverageTests.run
      "OpencodeSubagentCoverageTests.run", Async OpencodeSubagentCoverageTests.run
      "ReviewerLoopTests.run", Async ReviewerLoopTests.run ]
