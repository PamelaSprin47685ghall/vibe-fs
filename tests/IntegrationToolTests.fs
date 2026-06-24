module VibeFs.Tests.IntegrationToolTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.IntegrationCapsSpecs
open VibeFs.Tests.IntegrationKnowledgeGraphPreludeSpecs
open VibeFs.Tests.IntegrationAfterHookSpecs
open VibeFs.Tests.IntegrationMaintenanceSpecs
open VibeFs.Tests.IntegrationSubmitKnowledgeGraphSpecs
open VibeFs.Tests.IntegrationBookkeeperSpecs
open VibeFs.Tests.IntegrationToolDefSpecs
open VibeFs.Tests.IntegrationSubagentSpecs
open VibeFs.Tests.IntegrationMiscSpecs
open VibeFs.Tests.IntegrationMuxPreludeSpecs
open VibeFs.Tests.IntegrationMuxKnowledgeGraphSpecs
open VibeFs.Tests.IntegrationMuxTransformSpecs
open VibeFs.Tests.IntegrationMuxToolSpecs
open VibeFs.Tests.IntegrationMuxMethodologySpecs
open VibeFs.Tests.IntegrationMuxReviewSpecs
open VibeFs.Tests.IntegrationMuxReviewPromptSpecs

open VibeFs.Mux.Plugin
open VibeFs.Tests.IntegrationToolSetup


let run () : JS.Promise<unit> =
    promise {
        let reg = createRegistration (createObj [])
        wrapperSpec reg
        computeCountSpec reg
        let specs : (string * (unit -> JS.Promise<unit>)) list = [
            "buildCapsFileReadData", buildCapsFileReadDataSpec
            "capsTransform", capsTransformSpec
            "capsTransformInPlace", capsTransformInPlaceSpec
            "defaultPreludeWithoutCaps", defaultPreludeWithoutCapsSpec
            "capsAndMagicOrder", capsAndMagicOrderSpec
            "bookkeeperDoesNotReceiveCaps", bookkeeperDoesNotReceiveCapsSpec
            "compactionDoesNotReceiveCaps", compactionDoesNotReceiveCapsSpec
            "opencodeMethodologyProbe", opencodeMethodologyProbeSpec
            "opencodeMethodologyProbeBuildWithoutInputAgent", opencodeMethodologyProbeBuildWithoutInputAgentSpec
            "opencodeMethodologyProbeSuppressed", opencodeMethodologyProbeSuppressedSpec
            "opencodeMethodologyProbeExcludedAgents", opencodeMethodologyProbeExcludedAgentsSpec
            "opencodeMethodologyProbeStripped", opencodeMethodologyProbeStrippedSpec
            "knowledgeGraphPreludeWithoutCaps", knowledgeGraphPreludeWithoutCapsSpec
            "coderReceivesKnowledgeGraphPrelude", coderReceivesKnowledgeGraphPreludeSpec
            "browserDoesNotReceiveKnowledgeGraphPrelude", browserDoesNotReceiveKnowledgeGraphPreludeSpec
            "executorChildSessionWithoutInputAgentDoesNotReceiveKnowledgeGraphPrelude", executorChildSessionWithoutInputAgentDoesNotReceiveKnowledgeGraphPreludeSpec
            "fetchKnowledgeGraphSnapshot", fetchKnowledgeGraphSnapshotSpec
            "afterHookRecordsDirectWrite", afterHookRecordsDirectWriteSpec
            "afterHookSkipsChildSession", afterHookSkipsChildSessionSpec
            "afterHookSkipsFailedTool", afterHookSkipsFailedToolSpec
            "afterHookRecordsCoder", afterHookRecordsCoderSpec
            "afterHookRecordsExecutor", afterHookRecordsExecutorSpec
            "dailyMaintenanceLaunch", dailyMaintenanceLaunchSpec
            "heartbeatTriggersMaintenance", heartbeatTriggersMaintenanceSpec
            "heartbeatMaintenanceUsesParentSession", heartbeatMaintenanceUsesParentSessionSpec
            "heartbeatSchedulesOnlyEarliestDailyWhileAppendRuns", heartbeatSchedulesOnlyEarliestDailyWhileAppendRunsSpec
            "dailyRewriteTriggersNextDaily", dailyRewriteTriggersNextDailySpec
            "submitKnowledgeGraphAppend", submitKnowledgeGraphAppendSpec
            "submitKnowledgeGraphAppendEmpty", submitKnowledgeGraphAppendEmptySpec
            "submitKnowledgeGraphAppendDoesNotTriggerMaintenance", submitKnowledgeGraphAppendDoesNotTriggerMaintenanceSpec
            "submitKnowledgeGraphSchemaAllowsEmpty", submitKnowledgeGraphSchemaAllowsEmptySpec
            "submitKnowledgeGraphDailyRewrite", submitKnowledgeGraphDailyRewriteSpec
            "submitKnowledgeGraphReconstructsJobFromHistory", submitKnowledgeGraphReconstructsJobFromHistorySpec
            "bookkeeperLaunchCarriesAiSettings", bookkeeperLaunchCarriesAiSettingsSpec
            "bookkeeperFireAndForget", bookkeeperFireAndForgetSpec
            "websearchTriggersBookkeeper", websearchTriggersBookkeeperSpec
            "webfetchTriggersBookkeeper", webfetchTriggersBookkeeperSpec
            "bookkeeperSessionRegisteredInChildAgentRegistry", bookkeeperSessionRegisteredInChildAgentRegistrySpec
            "muxDailyMaintenanceLaunch", muxDailyMaintenanceLaunchSpec
            "muxDailyRewriteTriggersNext", muxDailyRewriteTriggersNextSpec
            "toolDefinition", toolDefinitionSpec
            "toolExecuteBefore", toolExecuteBeforeSpec
            "mimoApplyPatchExecuteBefore", mimoApplyPatchExecuteBeforeSpec
            "mimoTaskExecuteRoundTrip", mimoTaskExecuteRoundTripSpec
            "mimoTaskExecuteNestedReport", mimoTaskExecuteNestedReportSpec
            "mimoTaskExecuteInPlaceStrip", mimoTaskExecuteInPlaceStripSpec
            "mimoTaskExecuteStripsTaskId", mimoTaskExecuteStripsTaskIdSpec
            "mimoTaskDefinitionHandlesZodLikeParameters", mimoTaskDefinitionHandlesZodLikeParametersSpec
            "coderTool", coderToolSpec
            "investigatorTool", investigatorToolSpec
            "investigatorToolLateClientInjection", investigatorToolLateClientInjectionSpec
            "writeTool", (fun () -> writeToolSpec reg)
            "loopCommand", (fun () -> loopCommandSpec reg)
            "agentConfig", agentConfigSpec
            "bookkeeperAgentConfig", bookkeeperAgentConfigSpec
            "disableMimoMemoryAndCheckpoint", disableMimoMemoryAndCheckpointSpec
            "disableMimoMemoryAndCheckpointPreservesUserAgent", disableMimoMemoryAndCheckpointPreservesUserAgentSpec
            "applyAgentConfigForMimoDisablesWorkflow", applyAgentConfigForMimoDisablesWorkflowSpec
            "pluginConfigHookDisablesMimoMemoryAndCheckpoint", pluginConfigHookDisablesMimoMemoryAndCheckpointSpec
            "executorModeSchema", executorModeSchemaSpec
            "executorRejectsInvalidLanguage", executorRejectsInvalidLanguageSpec
            "executorActor", executorActorSpec
            "knowledgeGraphWorkspaceSerialization", knowledgeGraphWorkspaceSerializationSpec
            "knowledgeGraphPortLockTimeout", knowledgeGraphPortLockTimeoutSpec
            "muxFetchKnowledgeGraphSnapshot", muxFetchKnowledgeGraphSnapshotSpec
            "muxReturnBookkeeperAppend", muxReturnBookkeeperAppendSpec
            "muxReturnBookkeeperNoActiveJob", muxReturnBookkeeperNoActiveJobSpec
            "muxReturnBookkeeperReconstructsJobFromHistory", muxReturnBookkeeperReconstructsJobFromHistorySpec
            "muxReturnBookkeeperAppendDoesNotTriggerMaintenance", muxReturnBookkeeperAppendDoesNotTriggerMaintenanceSpec
            "muxDailyRewriteTriggersNext", muxDailyRewriteTriggersNextSpec
            "muxExecutorRwTriggersMaintenance", muxExecutorRwTriggersMaintenanceSpec
            "muxExecutorModeSchema", muxExecutorModeSchemaSpec
            "muxReadToolReturnsContent", muxReadToolReturnsContentSpec
            "muxReadToolListsDirectories", muxReadToolListsDirectoriesSpec
            "muxMessageTransformRegistered", muxMessageTransformRegisteredSpec
            "muxKnowledgeGraphPreludeForManager", muxKnowledgeGraphPreludeForManagerSpec
            "muxKnowledgeGraphPreludeForCoder", muxKnowledgeGraphPreludeForCoderSpec
            "muxNoKnowledgeGraphPreludeForExcludedAgents", muxNoKnowledgeGraphPreludeForExcludedAgentsSpec
            "muxCapsAndKnowledgeGraphPreludeOrder", muxCapsAndKnowledgeGraphPreludeOrderSpec
            "muxSummarization", (fun () -> promise { muxSummarizationSpec () })
            "muxSummarizationToolPolicy", (fun () -> promise { muxSummarizationToolPolicySpec () })
            "muxTopLevelPolicy", muxTopLevelPolicySpec
            "muxTopLevelDedup", muxTopLevelDedupSpec
            "muxMessagesTransformDedupsRepeatedRead", muxMessagesTransformDedupsRepeatedReadSpec
            "muxMessagesTransformDedupsRepeatedFileRead", muxMessagesTransformDedupsRepeatedFileReadSpec
            "muxMessagesTransformDedupsRepeatedReadForTopLevelExec", muxMessagesTransformDedupsRepeatedReadForTopLevelExecSpec
            "muxMessagesTransformAcceptedSubmitReviewEndsLoop", muxMessagesTransformAcceptedSubmitReviewEndsLoopSpec
            "muxTodoWriteWrapperSchema", muxTodoWriteWrapperSchemaSpec
            "muxTodoWriteCapturesCompletedWorkReport", muxTodoWriteCapturesCompletedWorkReportSpec
            "muxMagicTodoProjection", muxMagicTodoProjectionSpec
            "muxExecutorRoCatPrependsWarning", muxExecutorRoCatPrependsWarningSpec
            "muxMeditatorReadsFilesFromCwd", muxMeditatorReadsFilesFromCwdSpec
            "muxSubmitReviewNoActiveReview", muxSubmitReviewNoActiveReviewSpec
            "muxSubmitReviewPromptFormat", muxSubmitReviewPromptFormatSpec
            "muxAgentReportWrapperFormatsVerdict", muxAgentReportWrapperFormatsVerdictSpec
            "muxSubmitReviewUsesRolledBackHistoryTask", muxSubmitReviewUsesRolledBackHistoryTaskSpec
            "muxLoopReviewPromptUsesFrontMatter", muxLoopReviewPromptUsesFrontMatterSpec
            "muxSubmitReviewTwoRoundPassAccepts", muxSubmitReviewTwoRoundPassAcceptsSpec
            "muxSubmitReviewRejectKeepsReviewActive", muxSubmitReviewRejectKeepsReviewActiveSpec
            "muxSubmitReviewDoubleCheckReject", muxSubmitReviewDoubleCheckRejectSpec
            "muxSubmitReviewTerminatedCleansReviewState", muxSubmitReviewTerminatedCleansReviewStateSpec
            "muxExecutorFailureDoesNotBookkeep", muxExecutorFailureDoesNotBookkeepSpec
            "muxMethodologyProbeAppended", muxMethodologyProbeAppendedSpec
            "muxMethodologyProbeSuppressedAfterCall", muxMethodologyProbeSuppressedAfterCallSpec
            "muxMethodologyProbeExcludedAgents", muxMethodologyProbeExcludedAgentsSpec
            "muxMethodologyToolExecute", muxMethodologyToolExecuteSpec
            "muxMethodologyToolSchema", muxMethodologyToolSchemaSpec
            "muxMethodologyProbeStrippedOnReprojection", muxMethodologyProbeStrippedOnReprojectionSpec
        ]
        for (label, spec) in specs do
            do! timedAsync ("IntegrationTool." + label) spec
    }
