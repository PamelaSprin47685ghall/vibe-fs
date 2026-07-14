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

let integrationToolSpecs () : (string * (unit -> JS.Promise<unit>)) list =
    let reg = sharedMuxRegistration ()

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
      ("capsAndBacklogOrder", capsAndBacklogOrderSpec)
      ("capsEpochIsolationAndStability", capsEpochIsolationAndStabilitySpecs)
      ("toolDefinition", toolDefinitionSpec)
      "toolExecuteBefore", toolExecuteBeforeSpec
      "mimoApplyPatchExecuteBefore", mimoApplyPatchExecuteBeforeSpec
      "mimoTaskExecuteRoundTrip", mimoTaskExecuteRoundTripSpec
      "mimoTaskExecuteNestedReport", mimoTaskExecuteNestedReportSpec
      "mimoTaskExecuteInPlaceStrip", mimoTaskExecuteInPlaceStripSpec
      "mimoTaskExecuteStripsTaskId", mimoTaskExecuteStripsTaskIdSpec
      "mimoTaskDefinitionHandlesZodLikeParameters", mimoTaskDefinitionHandlesZodLikeParametersSpec
      "mimoTaskDefinitionRoutesEffectSchemaShapedParametersToJsonSchema",
      mimoTaskDefinitionRoutesEffectSchemaShapedParametersToJsonSchemaSpec
      "coderTool", coderToolSpec
      "investigatorTool", investigatorToolSpec
      "muxCoderInvalidIntents", muxCoderInvalidIntentsSpec
      "investigatorToolLateClientInjection", investigatorToolLateClientInjectionSpec
      "writeTool", (fun () -> writeToolSpec reg)
      "loopCommand", (fun () -> loopCommandSpec reg)
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
      "muxBacklogProjection", muxBacklogProjectionSpec
      "muxExecutorRoCatPrependsWarning", muxExecutorRoCatPrependsWarningSpec
      "muxSubmitReviewNoActiveReview", muxSubmitReviewNoActiveReviewSpec
      "muxSubmitReviewPromptFormat", muxSubmitReviewPromptFormatSpec
      "muxAgentReportWrapperFormatsVerdict", muxAgentReportWrapperFormatsVerdictSpec
      "muxSubmitReviewUsesRolledBackHistoryTask", muxSubmitReviewUsesRolledBackHistoryTaskSpec
      "muxLoopReviewPromptUsesFrontMatter", muxLoopReviewPromptUsesFrontMatterSpec
      "muxSubmitReviewTwoRoundPassAccepts", muxSubmitReviewTwoRoundPassAcceptsSpec
      "muxSubmitReviewReviseKeepsReviewActive", muxSubmitReviewReviseKeepsReviewActiveSpec
      "muxSubmitReviewDoubleCheckRevise", muxSubmitReviewDoubleCheckReviseSpec
      "muxSubmitReviewTerminatedCleansReviewState", muxSubmitReviewTerminatedCleansReviewStateSpec
      "muxSubmitReviewWipSkipsReviewer", muxSubmitReviewWipSkipsReviewerSpec
      "muxSubmitReviewOmittedWipSkipsReviewer", muxSubmitReviewOmittedWipSkipsReviewerSpec
      "muxTodoWriteMethodologySchema", muxTodoWriteMethodologySchemaSpec
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
      ("muxStreamEndToolCallAsTextTriggersFallback", muxStreamEndToolCallAsTextTriggersFallbackSpec) ]
