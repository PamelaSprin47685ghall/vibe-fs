module VibeFs.Tests.IntegrationToolTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.IntegrationCapsSpecs
open VibeFs.Tests.IntegrationWikiPreludeSpecs
open VibeFs.Tests.IntegrationAfterHookSpecs
open VibeFs.Tests.IntegrationMaintenanceSpecs
open VibeFs.Tests.IntegrationSubmitWikiSpecs
open VibeFs.Tests.IntegrationBookkeeperSpecs
open VibeFs.Tests.IntegrationToolDefSpecs
open VibeFs.Tests.IntegrationSubagentSpecs
open VibeFs.Tests.IntegrationMiscSpecs
open VibeFs.Kernel.Dyn
open VibeFs.Mux.Plugin

let wrapperSpec (reg: obj) =
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let targets = wrappers |> Array.map (fun w -> str w "targetTool") |> Array.sort
    let expected = [| "agent_report"; "file_edit_insert"; "file_edit_replace_string"; "file_read"; "todo_write" |] |> Array.sort
    check "wrapper targets correct" (targets = expected)
    let ar = wrappers |> Array.find (fun w -> str w "targetTool" = "agent_report")
    check "agent_report wrapper exists" (not (isNullish ar))

let computeCountSpec (reg: obj) =
    let tools = unbox<obj[]> (get reg "tools")
    let names = tools |> Array.map (fun t -> str t "name")
    check "has coder tool" (names |> Array.contains "coder")
    check "has webfetch tool" (names |> Array.contains "webfetch")
    check "has write tool" (names |> Array.contains "write")
    check "has read tool" (names |> Array.contains "read")
    check "has submit_review tool" (names |> Array.contains "submit_review")

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
            "wikiPreludeWithoutCaps", wikiPreludeWithoutCapsSpec
            "coderReceivesWikiPrelude", coderReceivesWikiPreludeSpec
            "browserDoesNotReceiveWikiPrelude", browserDoesNotReceiveWikiPreludeSpec
            "fetchWikiSnapshot", fetchWikiSnapshotSpec
            "afterHookRecordsDirectWrite", afterHookRecordsDirectWriteSpec
            "afterHookSkipsChildSession", afterHookSkipsChildSessionSpec
            "afterHookSkipsFailedTool", afterHookSkipsFailedToolSpec
            "afterHookRecordsCoder", afterHookRecordsCoderSpec
            "afterHookRecordsExecutor", afterHookRecordsExecutorSpec
            "dailyMaintenanceLaunch", dailyMaintenanceLaunchSpec
            "weeklyMaintenanceLaunch", weeklyMaintenanceLaunchSpec
            "weeklyMaintenanceUsesLastSunday", weeklyMaintenanceUsesLastSundaySpec
            "weeklyMaintenanceWithoutSnapshotFile", weeklyMaintenanceWithoutSnapshotFileSpec
            "heartbeatTriggersMaintenance", heartbeatTriggersMaintenanceSpec
            "submitWikiAppend", submitWikiAppendSpec
            "submitWikiAppendEmpty", submitWikiAppendEmptySpec
            "submitWikiSchemaAllowsEmpty", submitWikiSchemaAllowsEmptySpec
            "submitWikiDailyRewrite", submitWikiDailyRewriteSpec
            "submitWikiWeeklyRewrite", submitWikiWeeklyRewriteSpec
            "submitWikiReconstructsJobFromHistory", submitWikiReconstructsJobFromHistorySpec
            "bookkeeperLaunchCarriesAiSettings", bookkeeperLaunchCarriesAiSettingsSpec
            "bookkeeperFireAndForget", bookkeeperFireAndForgetSpec
            "websearchTriggersBookkeeper", websearchTriggersBookkeeperSpec
            "webfetchTriggersBookkeeper", webfetchTriggersBookkeeperSpec
            "bookkeeperSessionRegisteredInChildAgentRegistry", bookkeeperSessionRegisteredInChildAgentRegistrySpec
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
            "executorModeSchema", executorModeSchemaSpec
            "executorActor", executorActorSpec
            "wikiWorkspaceSerialization", wikiWorkspaceSerializationSpec
            "wikiPortLockTimeout", wikiPortLockTimeoutSpec
        ]
        for (label, spec) in specs do
            do! timedAsync ("IntegrationTool." + label) spec
    }
