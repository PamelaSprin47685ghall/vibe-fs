/// Flat catalog of IntegrationTool integration specs (one runner entry per spec).
module Wanxiangshu.Tests.IntegrationToolSpecCatalog

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.IntegrationCapsSpecs
open Wanxiangshu.Tests.IntegrationCapsSpecsSubagent
open Wanxiangshu.Tests.IntegrationCapsSpecsSubagentMux
open Wanxiangshu.Tests.IntegrationToolDefSpecs
open Wanxiangshu.Tests.IntegrationToolDefSpecsMimo
open Wanxiangshu.Tests.IntegrationSubagentSpecs
open Wanxiangshu.Tests.IntegrationMiscSpecs
open Wanxiangshu.Tests.IntegrationMiscSpecsAgent
open Wanxiangshu.Tests.IntegrationMuxPreludeSpecs
open Wanxiangshu.Tests.IntegrationMuxTransformSpecs
open Wanxiangshu.Tests.IntegrationMuxToolSpecs
open Wanxiangshu.Tests.IntegrationMuxToolSpecsTodo
open Wanxiangshu.Tests.IntegrationMuxToolSpecsRead
open Wanxiangshu.Tests.IntegrationMuxToolSpecsHooks
open Wanxiangshu.Tests.IntegrationMuxToolSpecsHooksNudge
open Wanxiangshu.Tests.IntegrationMuxMethodologySpecs
open Wanxiangshu.Tests.IntegrationMuxReviewSpecs
open Wanxiangshu.Tests.IntegrationMuxReviewPromptSpecs
open Wanxiangshu.Tests.IntegrationMuxFallbackSpecs
open Wanxiangshu.Tests.IntegrationToolSetup

let private integrationToolSpecsGroup1 (reg: obj) : (string * (unit -> JS.Promise<unit>)) list =
    [ "wrapperSpec", (fun () -> promise { wrapperSpec reg })
      "computeCountSpec", (fun () -> promise { computeCountSpec reg })
      "subagentCapsInjection", subagentCapsInjectionSpec
      "crossSessionIsolation", crossSessionIsolationSpec
      ("opencodeSubsessionParentID", opencodeSubsessionParentIDSpec)
      ("subagentFallbackRawText", subagentFallbackRawTextSpec)
      ("muxSubsessionParentID", muxSubsessionParentIDSpec)
      ("buildCapsFileReadData", buildCapsFileReadDataSpec)
      ("capsTransform", capsTransformSpec)
      ("capsTransformInPlace", capsTransformInPlaceSpec)
      ("defaultPreludeWithoutCaps", defaultPreludeWithoutCapsSpec)
      ("capsOrder", capsOrderSpec)
      ("capsEpochIsolationAndStability", capsEpochIsolationAndStabilitySpecs)
      "coderTool", coderToolSpec
      "inspectorTool", inspectorToolSpec
      "muxCoderInvalidIntents", muxCoderInvalidIntentsSpec
      "inspectorToolLateClientInjection", inspectorToolLateClientInjectionSpec
      "inspectorToolWithHostConfiguredModel", inspectorToolWithHostConfiguredModelSpec
      "writeTool", (fun () -> writeToolSpec reg)
      "loopCommand", loopCommandSpec
      "agentConfig", agentConfigSpec
      "disableMimoMemoryAndCheckpoint", disableMimoMemoryAndCheckpointSpec
      "disableMimoMemoryAndCheckpointPreservesUserAgent", disableMimoMemoryAndCheckpointPreservesUserAgentSpec
      "applyAgentConfigForMimoDisablesWorkflow", applyAgentConfigForMimoDisablesWorkflowSpec
      "pluginConfigHookDisablesMimoMemoryAndCheckpoint", pluginConfigHookDisablesMimoMemoryAndCheckpointSpec
      "muxReadToolReturnsContent", muxReadToolReturnsContentSpec
      "muxReadToolListsDirectories", muxReadToolListsDirectoriesSpec
      "muxMessageTransformRegistered", muxMessageTransformRegisteredSpec
      "muxSummarization", (fun () -> promise { muxSummarizationSpec () })
      "muxSummarizationToolPolicy", (fun () -> promise { muxSummarizationToolPolicySpec () })
      "muxTopLevelPolicy", muxTopLevelPolicySpec
      "muxMessagesTransformAcceptedSubmitReviewEndsLoop", muxMessagesTransformAcceptedSubmitReviewEndsLoopSpec
      "muxTodoWriteWrapperSchema", muxTodoWriteWrapperSchemaSpec
      "muxTodoWriteCapturesCompletedWorkReport", muxTodoWriteCapturesCompletedWorkReportSpec
      "muxTodoProjection", muxTodoProjectionSpec
      "muxExecutorRoCatPrependsWarning", muxExecutorRoCatPrependsWarningSpec
      "muxSubmitReviewNoActiveReview", muxSubmitReviewNoActiveReviewSpec ]

let private integrationToolSpecsGroup2 (reg: obj) : (string * (unit -> JS.Promise<unit>)) list =
    [ "muxSubmitReviewPromptFormat", muxSubmitReviewPromptFormatSpec
      "muxAgentReportWrapperFormatsVerdict", muxAgentReportWrapperFormatsVerdictSpec
      "muxSubmitReviewUsesRolledBackHistoryTask", muxSubmitReviewUsesRolledBackHistoryTaskSpec
      "muxSubmitReviewTwoRoundPassAccepts", muxSubmitReviewTwoRoundPassAcceptsSpec
      "muxSubmitReviewReviseKeepsReviewActive", muxSubmitReviewReviseKeepsReviewActiveSpec
      "muxSubmitReviewDoubleCheckRevise", muxSubmitReviewDoubleCheckReviseSpec
      "muxSubmitReviewTerminatedCleansReviewState", muxSubmitReviewTerminatedCleansReviewStateSpec
      "muxSubmitReviewWipSkipsReviewer", muxSubmitReviewWipSkipsReviewerSpec
      "muxSubmitReviewOmittedWipSkipsReviewer", muxSubmitReviewOmittedWipSkipsReviewerSpec
      "muxEventHookAbortDeactivatesReview", muxEventHookAbortDeactivatesReviewSpec
      "muxToolExecuteBeforeSetsUiLabel", muxToolExecuteBeforeSetsUiLabelSpec
      ("muxSystemTransformClearsOutputLength", muxSystemTransformClearsOutputLengthSpec)
      ("muxToolSchemasAreCleanStaticallyButInjectedDynamically",
       muxToolSchemasAreCleanStaticallyButInjectedDynamicallySpec)
      ("muxLoopSlashCommandWritesEventLogUnderDepsDirectory", muxLoopSlashCommandWritesEventLogUnderDepsDirectorySpec)
      ("muxToolExecuteAfterBlocksRepeatedIdenticalCall", muxToolExecuteAfterBlocksRepeatedIdenticalCallSpec)
      ("muxToolExecuteAfterBlocksRepeatedCallIgnoringControls",
       muxToolExecuteAfterBlocksRepeatedCallIgnoringControlsSpec)
      ("muxToolExecuteAfterMapsNetworkError", muxToolExecuteAfterMapsNetworkErrorSpec)
      ("muxStreamEndToolUseErrorTriggersNudge", muxStreamEndToolUseErrorTriggersNudgeSpec)
      ("muxStreamEndToolCallsDoesNotTriggerNudge", muxStreamEndToolCallsDoesNotTriggerNudgeSpec)
      ("muxSessionErrorTriggersFallbackContinue", muxSessionErrorTriggersFallbackContinueSpec)
      ("muxStreamEndToolCallAsTextTriggersFallback", muxStreamEndToolCallAsTextTriggersFallbackSpec)
      ("muxAbortRunThrowsAbortUnavailable", muxAbortRunThrowsAbortUnavailableSpec)
      ("muxNudgeBooleanTrueReturnsAcceptanceUnknown", muxNudgeBooleanTrueReturnsAcceptanceUnknownSpec)
      ("muxNudgeValidReceiptReturnsDelivered", muxNudgeValidReceiptReturnsDeliveredSpec)
      ("muxNudgeMismatchedReceiptReturnsFailed", muxNudgeMismatchedReceiptReturnsFailedSpec)
      ("muxContinueBooleanTrueRejectsAcceptanceUnknown", muxContinueBooleanTrueRejectsAcceptanceUnknownSpec)
      ("muxContinueValidReceiptResolves", muxContinueValidReceiptResolvesSpec)
      ("muxAbortUnavailableNudgeFlow", muxAbortUnavailableNudgeFlowSpec)
      ("muxAbortUnavailableDoesNotReportCancelled", muxAbortUnavailableDoesNotReportCancelledSpec)
      ("muxAcceptanceUnknownKeepsNudgeLease", muxAcceptanceUnknownKeepsNudgeLeaseSpec)
      ("muxOneInFlightSecondContinueRejected", muxOneInFlightSecondContinueRejectedSpec)
      ("muxLogicalReceiptBooleanIsNotAccepted", muxLogicalReceiptBooleanIsNotAcceptedSpec)
      ("muxLogicalReceiptValidMapsToUserMessage", muxLogicalReceiptValidMapsToUserMessageSpec)
      ("muxNudgeMissingHelpersReturnsFailed", muxNudgeMissingHelpersReturnsFailedSpec)
      ("muxNudgeMissingNudgeReturnsFailed", muxNudgeMissingNudgeReturnsFailedSpec)
      ("muxNudgeNonFunctionNudgeReturnsFailed", muxNudgeNonFunctionNudgeReturnsFailedSpec)
      ("muxContinueMissingHelpersRejectsFailed", muxContinueMissingHelpersRejectsFailedSpec)
      ("muxContinueMissingNudgeRejectsFailed", muxContinueMissingNudgeRejectsFailedSpec)
      ("muxContinueNonFunctionNudgeRejectsFailed", muxContinueNonFunctionNudgeRejectsFailedSpec)
      ("muxContinueMismatchedReceiptRejectsFailed", muxContinueMismatchedReceiptRejectsFailedSpec)
      ("muxRecoverWithPromptMissingNudgeRejectsFailed", muxRecoverWithPromptMissingNudgeRejectsFailedSpec) ]

let integrationToolSpecs () : (string * (unit -> JS.Promise<unit>)) list =
    let reg = sharedMuxRegistration ()
    integrationToolSpecsGroup1 reg @ integrationToolSpecsGroup2 reg
