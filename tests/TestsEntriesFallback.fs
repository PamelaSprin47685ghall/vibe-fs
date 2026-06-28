module Wanxiangshu.Tests.TestsEntriesFallback
open Wanxiangshu.Tests.TestsTestBody
open Wanxiangshu.Tests.FallbackKernelTests
open Wanxiangshu.Tests.FallbackConfigCodecTests
open Wanxiangshu.Tests.FallbackRuntimeStateTests
open Wanxiangshu.Tests.FallbackEventBridgeTests
open Wanxiangshu.Tests.FallbackEventBridgeToolTextTests
open Wanxiangshu.Tests.FallbackMessageCodecTests
open Wanxiangshu.Tests.FallbackIntegrationTests
open Wanxiangshu.Tests.ArchitectureTestsFallback
open Wanxiangshu.Tests.WebFetchGuardTests
open Wanxiangshu.Tests.ReviewVerdictTests
open Wanxiangshu.Tests.ToolCopyTests
open Wanxiangshu.Tests.JsArrayMutateTests

let fallbackTestEntries () : (string * TestBody) list =
    [
    "FallbackKernelTests.run", Sync (sync FallbackKernelTests.run)
    "FallbackConfigCodecTests.run", Sync (sync FallbackConfigCodecTests.run)
    "FallbackRuntimeStateTests.run", Sync (sync FallbackRuntimeStateTests.run)
    "FallbackEventBridgeTests.run", Async FallbackEventBridgeTests.run
    "FallbackEventBridgeToolTextTests.run", Async FallbackEventBridgeToolTextTests.run
    "FallbackMessageCodecTests.run", Sync (sync FallbackMessageCodecTests.run)
    "FallbackIntegrationTests.run", Sync (sync FallbackIntegrationTests.run)
    "Arch.Fallback.zeroTimer", Sync (sync ArchitectureTestsFallback.zeroTimer)
    "Arch.Fallback.kernelPurity", Sync (sync ArchitectureTestsFallback.kernelPurity)
    "Arch.Fallback.ompFallbackIsolation", Sync (sync ArchitectureTestsFallback.ompFallbackIsolation)
    "Arch.Fallback.configSsot", Sync (sync ArchitectureTestsFallback.configSsot)
    ]

let tailTestEntries () : (string * TestBody) list =
    [
    "WebFetchGuardTests.run", Sync (sync WebFetchGuardTests.run)
    "ReviewVerdictTests.run", Sync (sync ReviewVerdictTests.run)
    "ToolCopyTests.run", Sync (sync ToolCopyTests.run)
    "JsArrayMutateTests.run", Sync (sync JsArrayMutateTests.run)
    ]

