module Wanxiangshu.Tests.TestsEntriesFallback

open Wanxiangshu.Tests.TestsTestBody
open Wanxiangshu.Tests.FallbackKernelTests
open Wanxiangshu.Tests.FallbackConfigCodecTests
open Wanxiangshu.Tests.FallbackRuntimeStateTests
open Wanxiangshu.Tests.FallbackEventBridgeTests
open Wanxiangshu.Tests.FallbackMessageCodecTests
open Wanxiangshu.Tests.FallbackIntegrationTests
open Wanxiangshu.Tests.FallbackRecoveryWaitTests
open Wanxiangshu.Tests.SubagentIoFallbackRecoveryTests
open Wanxiangshu.Tests.SubagentIoFallbackRecoveryTestsPart2
open Wanxiangshu.Tests.SubagentIoFallbackRecoveryTestsPart3
open Wanxiangshu.Tests.ArchitectureTestsFallback
open Wanxiangshu.Tests.WebFetchGuardTests
open Wanxiangshu.Tests.ReviewVerdictTests
open Wanxiangshu.Tests.ToolCopyTests
open Wanxiangshu.Tests.JsArrayMutateTests
open Wanxiangshu.Tests.FallbackAgentAndModelTests
open Wanxiangshu.Tests.FallbackAgentAndModelPart2Tests

let fallbackTestEntries () : (string * TestBody) list =
    [ "FallbackKernelTests.run", Sync(sync FallbackKernelTests.run)
      "FallbackConfigCodecTests.run", Sync(sync FallbackConfigCodecTests.run)
      "FallbackRuntimeStateTests.run", Sync(sync FallbackRuntimeStateTests.run)
      "FallbackEventBridgeTests.run", Async FallbackEventBridgeTests.run
      "FallbackAgentAndModelTests.run", Async FallbackAgentAndModelTests.run
      "FallbackAgentAndModelPart2Tests.run", Async FallbackAgentAndModelPart2Tests.run
      "FallbackMessageCodecTests.run", Sync(sync FallbackMessageCodecTests.run)
      "FallbackIntegrationTests.run", Sync(sync FallbackIntegrationTests.run)
      "FallbackRecoveryWaitTests.run", Async FallbackRecoveryWaitTests.run
      "SubagentIoFallbackRecoveryTests.run", Async SubagentIoFallbackRecoveryTests.run
      "SubagentIoFallbackRecoveryTestsPart2.run", Async SubagentIoFallbackRecoveryTestsPart2.run
      "SubagentIoFallbackRecoveryTestsPart3.run", Async SubagentIoFallbackRecoveryTestsPart3.run
      "Arch.Fallback.zeroTimer", Sync(sync ArchitectureTestsFallback.zeroTimer)
      "Arch.Fallback.kernelPurity", Sync(sync ArchitectureTestsFallback.kernelPurity)
      "Arch.Fallback.ompFallbackIsolation", Sync(sync ArchitectureTestsFallback.ompFallbackIsolation)
      "Arch.Fallback.configSsot", Sync(sync ArchitectureTestsFallback.configSsot) ]

let tailTestEntries () : (string * TestBody) list =
    [ "WebFetchGuardTests.run", Sync(sync WebFetchGuardTests.run)
      "ReviewVerdictTests.run", Sync(sync ReviewVerdictTests.run)
      "ToolCopyTests.run", Sync(sync ToolCopyTests.run)
      "JsArrayMutateTests.run", Sync(sync JsArrayMutateTests.run) ]
