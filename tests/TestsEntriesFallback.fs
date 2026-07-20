module Wanxiangshu.Tests.TestsEntriesFallback

open Wanxiangshu.Tests.TestsTestBody
open Wanxiangshu.Tests.FallbackKernelTests
open Wanxiangshu.Tests.FallbackConfigCodecTests
open Wanxiangshu.Tests.FallbackConfigShapeTests
open Wanxiangshu.Tests.FallbackRuntimeStoreTests
open Wanxiangshu.Tests.FallbackEventBridgeTests
open Wanxiangshu.Tests.FallbackMessageCodecTests
open Wanxiangshu.Tests.FallbackIntegrationTests
open Wanxiangshu.Tests.FallbackRecoveryWaitTests
open Wanxiangshu.Tests.FallbackRequestedRecoveryTests
open Wanxiangshu.Tests.WebFetchGuardTests
open Wanxiangshu.Tests.ReviewVerdictTests
open Wanxiangshu.Tests.ToolCopyTests
open Wanxiangshu.Tests.JsArrayMutateTests
open Wanxiangshu.Tests.FallbackAgentAndModelTests
open Wanxiangshu.Tests.FallbackAgentAndModelInjectionTests
open Wanxiangshu.Tests.SubsessionDecisionTests
open Wanxiangshu.Tests.OpencodeSubsessionHostAdapterModelTests
open Wanxiangshu.Tests.SubsessionScenarioTests
open Wanxiangshu.Tests.SubsessionActorPumpTests
open Wanxiangshu.Tests.SubsessionDispatchFailureTests
open Wanxiangshu.Tests.SubsessionConcurrentCancelTests
open Wanxiangshu.Tests.SubsessionTranscriptBoundaryTests
open Wanxiangshu.Tests.SubsessionPhysicalIsolationTests
open Wanxiangshu.Tests.SubsessionEvidenceRaceTests
open Wanxiangshu.Tests.OpencodeFallbackChildIdleTests
open Wanxiangshu.Tests.SubagentCompactionRegressionTests
open Wanxiangshu.Tests.FallbackHooksHelperAgentModelTests
open Wanxiangshu.Tests.SubagentOutputTests
open Wanxiangshu.Tests.SubsessionEmptyOutputContinueTests
open Wanxiangshu.Tests.DispatchRegistryTests
open Wanxiangshu.Tests.SessionActorTests
open Wanxiangshu.Tests.FallbackLeaseValidationRulesTests
open Wanxiangshu.Tests.FallbackLeasePureTests
open Wanxiangshu.Tests.FallbackPropertyPureTests
open Wanxiangshu.Tests.NudgeErrorClassificationTests
open Wanxiangshu.Tests.RetryDispatchGovernorTests
open Wanxiangshu.Tests.NudgeOwnerDiagnosticTests

let fallbackTestEntries () : (string * TestBody) list =
    [ "FallbackKernelTests.run", Sync(sync FallbackKernelTests.run)
      "FallbackConfigCodecTests.run", Sync(sync FallbackConfigCodecTests.run)
      "FallbackConfigShapeTests.run", Sync(sync FallbackConfigShapeTests.run)
      "FallbackRuntimeStoreTests.run", Sync(sync FallbackRuntimeStoreTests.run)
      "FallbackEventBridgeTests.run", Async FallbackEventBridgeTests.run
      "FallbackAgentAndModelTests.run", Async FallbackAgentAndModelTests.run
      "FallbackAgentAndModelInjectionTests.run", Async FallbackAgentAndModelInjectionTests.run
      "FallbackMessageCodecTests.run", Sync(sync FallbackMessageCodecTests.run)
      "FallbackIntegrationTests.run", Sync(sync FallbackIntegrationTests.run)
      "FallbackRecoveryWaitTests.run", Async FallbackRecoveryWaitTests.run
      "FallbackRequestedRecoveryTests.run", Async FallbackRequestedRecoveryTests.run
      "RetryDispatchGovernorTests.run", Async RetryDispatchGovernorTests.run
      "SubsessionDecisionTests.run", Sync(sync SubsessionDecisionTests.run)
      "OpencodeSubsessionHostAdapterModelTests.run", Sync(sync OpencodeSubsessionHostAdapterModelTests.run)
      "SubsessionScenarioTests.run", Sync(sync SubsessionScenarioTests.run)
      "SubsessionActorPumpTests.run", Async SubsessionActorPumpTests.run
      "SubsessionDispatchFailureTests.run", Async SubsessionDispatchFailureTests.run
      "DispatchRegistryTests.run", Async DispatchRegistryTests.run
      "SessionActorTests.run", Async SessionActorTests.run
      "SubsessionConcurrentCancelTests.run", Async SubsessionConcurrentCancelTests.run
      "SubsessionTranscriptBoundaryTests.run", Sync(sync SubsessionTranscriptBoundaryTests.run)
      "SubsessionPhysicalIsolationTests.run", Async SubsessionPhysicalIsolationTests.run
      "SubsessionEvidenceRaceTests.run", Sync(sync SubsessionEvidenceRaceTests.run)
      "OpencodeFallbackChildIdleTests.run", Async OpencodeFallbackChildIdleTests.run
      "SubagentCompactionRegressionTests.run", Sync(sync SubagentCompactionRegressionTests.run)
      "FallbackHooksHelperAgentModelTests.run", Async FallbackHooksHelperAgentModelTests.run
      "SubagentOutputTests.run", Sync(sync SubagentOutputTests.run)
      "SubsessionEmptyOutputContinueTests.run", Sync(sync SubsessionEmptyOutputContinueTests.run)
      "FallbackLeaseValidationRulesTests.run", Sync(sync FallbackLeaseValidationRulesTests.run)
      "FallbackLeasePureTests.run", Sync(sync FallbackLeasePureTests.run)
      "FallbackPropertyPureTests.run", Sync(sync FallbackPropertyPureTests.run)
      "NudgeErrorClassificationTests.run", Sync(sync NudgeErrorClassificationTests.run)
      "NudgeOwnerDiagnosticTests.run", Sync(sync NudgeOwnerDiagnosticTests.run) ]

let tailTestEntries () : (string * TestBody) list =
    [ "WebFetchGuardTests.run", Sync(sync WebFetchGuardTests.run)
      "ReviewVerdictTests.run", Sync(sync ReviewVerdictTests.run)
      "ToolCopyTests.run", Sync(sync ToolCopyTests.run)
      "JsArrayMutateTests.run", Sync(sync JsArrayMutateTests.run) ]
