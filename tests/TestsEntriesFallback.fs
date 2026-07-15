module Wanxiangshu.Tests.TestsEntriesFallback

open Wanxiangshu.Tests.TestsTestBody
open Wanxiangshu.Tests.FallbackKernelTests
open Wanxiangshu.Tests.FallbackConfigCodecTests
open Wanxiangshu.Tests.FallbackRuntimeStateTests
open Wanxiangshu.Tests.FallbackEventBridgeTests
open Wanxiangshu.Tests.FallbackMessageCodecTests
open Wanxiangshu.Tests.FallbackIntegrationTests
open Wanxiangshu.Tests.FallbackRecoveryWaitTests
open Wanxiangshu.Tests.WebFetchGuardTests
open Wanxiangshu.Tests.ReviewVerdictTests
open Wanxiangshu.Tests.ToolCopyTests
open Wanxiangshu.Tests.JsArrayMutateTests
open Wanxiangshu.Tests.FallbackAgentAndModelTests
open Wanxiangshu.Tests.FallbackAgentAndModelPart2Tests
open Wanxiangshu.Tests.FallbackInjectionFoldTests
open Wanxiangshu.Tests.SubsessionDecisionTests
open Wanxiangshu.Tests.OpencodeSubsessionHostAdapterModelTests
open Wanxiangshu.Tests.SubsessionScenarioTests
open Wanxiangshu.Tests.SubsessionActorPumpTests
open Wanxiangshu.Tests.SubsessionV36HardTests
open Wanxiangshu.Tests.SubsessionV37HardTests
open Wanxiangshu.Tests.SubsessionV38DecisionTests
open Wanxiangshu.Tests.SubsessionV40HardTests
open Wanxiangshu.Tests.SubsessionEvidenceRaceTests
open Wanxiangshu.Tests.FallbackHooksHelperAgentModelTests

let fallbackTestEntries () : (string * TestBody) list =
    [ "FallbackKernelTests.run", Sync(sync FallbackKernelTests.run)
      "FallbackConfigCodecTests.run", Sync(sync FallbackConfigCodecTests.run)
      "FallbackRuntimeStateTests.run", Sync(sync FallbackRuntimeStateTests.run)
      "FallbackEventBridgeTests.run", Async FallbackEventBridgeTests.run
      "FallbackAgentAndModelTests.run", Async FallbackAgentAndModelTests.run
      "FallbackAgentAndModelPart2Tests.run", Async FallbackAgentAndModelPart2Tests.run
      "FallbackInjectionFoldTests.run", Sync(sync FallbackInjectionFoldTests.run)
      "FallbackMessageCodecTests.run", Sync(sync FallbackMessageCodecTests.run)
      "FallbackIntegrationTests.run", Sync(sync FallbackIntegrationTests.run)
      "FallbackRecoveryWaitTests.run", Async FallbackRecoveryWaitTests.run
      "SubsessionDecisionTests.run", Sync(sync SubsessionDecisionTests.run)
      "OpencodeSubsessionHostAdapterModelTests.run", Sync(sync OpencodeSubsessionHostAdapterModelTests.run)
      "SubsessionScenarioTests.run", Sync(sync SubsessionScenarioTests.run)
      "SubsessionActorPumpTests.run", Async SubsessionActorPumpTests.run
      "SubsessionV36HardTests.run", Async SubsessionV36HardTests.run
      "SubsessionV37HardTests.run", Async SubsessionV37HardTests.run
      "SubsessionV38DecisionTests.run", Sync(sync SubsessionV38DecisionTests.run)
      "SubsessionV40HardTests.run", Async SubsessionV40HardTests.run
      "SubsessionEvidenceRaceTests.run", Sync(sync SubsessionEvidenceRaceTests.run)
      "FallbackHooksHelperAgentModelTests.run", Async FallbackHooksHelperAgentModelTests.run ]

let tailTestEntries () : (string * TestBody) list =
    [ "WebFetchGuardTests.run", Sync(sync WebFetchGuardTests.run)
      "ReviewVerdictTests.run", Sync(sync ReviewVerdictTests.run)
      "ToolCopyTests.run", Sync(sync ToolCopyTests.run)
      "JsArrayMutateTests.run", Sync(sync JsArrayMutateTests.run) ]
