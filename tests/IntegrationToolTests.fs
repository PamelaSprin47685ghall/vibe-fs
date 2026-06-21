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
open VibeFs.Kernel.Wiki
open VibeFs.Mux.Plugin
open VibeFs.Shell.WikiFiles
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Tests.TempWorkspace

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
    check "has fetch_wiki tool" (names |> Array.contains "fetch_wiki")
    check "has return_bookkeeper tool" (names |> Array.contains "return_bookkeeper")

let muxFetchWikiSnapshotSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-wiki-fetch-"
    do! unbox<JS.Promise<unit>> (fsAsync?mkdir(pathModule?join(workspaceDir, "wiki"), box {| recursive = true |}))
    let snapshotFile = unbox<string> (pathModule?join(workspaceDir, "wiki", "snapshot.ndjson"))
    do! writeFileAsync snapshotFile (renderNdjson (SnapshotHeader(Some "2026-06-14")) [ wikiEntry "0a3f" "项目插件入口在哪里？" "Snapshot answer" ])
    let reg = createRegistration (createObj [])
    let fetchTool = muxToolByName reg "fetch_wiki"
    if isNullish fetchTool then
        check "mux registration exposes fetch_wiki tool" false
    else
        let context = createObj [ "directory", box workspaceDir; "sessionID", box "mux-wiki-fetch-session" ]
        let! answer = ((get fetchTool "execute") $ (context, createObj [ "id", box "0a3f" ])) |> unbox<JS.Promise<string>>
        check "mux fetch_wiki returns snapshot answer" (answer = "Snapshot answer")
        let! invalid = ((get fetchTool "execute") $ (context, createObj [ "id", box "nope" ])) |> unbox<JS.Promise<string>>
        check "mux fetch_wiki validates id format" (invalid.Contains "Invalid wiki id")
        let! missing = ((get fetchTool "execute") $ (context, createObj [ "id", box "b912" ])) |> unbox<JS.Promise<string>>
        check "mux fetch_wiki reports missing snapshot entry" (missing.Contains "Wiki entry not found in this session snapshot")
    do! rmAsync workspaceDir
}

let muxReturnBookkeeperAppendSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-wiki-submit-"
    do! ensureWikiDir workspaceDir
    let snapshotPath = unbox<string> (pathModule?join(workspaceDir, "wiki", "snapshot.ndjson"))
    do! writeFileAsync snapshotPath (renderNdjson (SnapshotHeader(Some "2026-06-14")) [ wikiEntry "0a3f" "项目插件入口在哪里？" "Old answer" ])
    let reg = createRegistration (createObj [])
    let submitTool = muxToolByName reg "return_bookkeeper"
    if isNullish submitTool then
        check "mux registration exposes return_bookkeeper tool" false
    else
        let runtime = muxWikiRuntime reg
        if isNullish runtime then
            check "mux registration exposes wiki runtime for testing" false
        else
            let today = System.DateTime.UtcNow.ToString("yyyy-MM-dd")
            registerMuxWikiJobForTest reg "mux-wiki-job" workspaceDir "append" (createObj [ "today", box today ])
            let entries = [|
                wikiDraftEntry (Some "0a3f") "项目插件入口在哪里？" "Updated answer"
                wikiDraftEntry None "新知识？" "Fresh answer"
            |]
            let submitArgs = createObj [ "entries", box entries ]
            let submitCtx = createObj [ "directory", box workspaceDir; "sessionID", box "mux-wiki-job" ]
            let! result = ((get submitTool "execute") $ (submitCtx, submitArgs)) |> unbox<JS.Promise<string>>
            check "mux return_bookkeeper returns a response" (result <> "")
            let! projection = readProjectionAsync workspaceDir
            let updated = Map.find (tryParseId "0a3f" |> Option.get) projection
            check "mux return_bookkeeper updates existing id" (updated.a = "Updated answer")
            let fresh = projection |> Map.toList |> List.tryFind (fun (_, entry) -> entry.q = "新知识？")
            check "mux return_bookkeeper allocates id for new entry" (
                match fresh with
                | Some (id, entry) -> idValue id <> "" && entry.a = "Fresh answer"
                | None -> false)
            let! files = readAllWikiFiles workspaceDir
            let dayFile = files |> List.tryFind (fun file -> match file.header with DayHeader(date, _) -> date = today | _ -> false)
            check "mux return_bookkeeper creates today file" (dayFile.IsSome)
    do! rmAsync workspaceDir
}

let muxReturnBookkeeperNoActiveJobSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-wiki-nojob-"
    do! ensureWikiDir workspaceDir
    let reg = createRegistration (createObj [])
    let submitTool = muxToolByName reg "return_bookkeeper"
    if isNullish submitTool then
        check "mux registration exposes return_bookkeeper tool" false
    else
        let entries = [| wikiDraftEntry None "问题？" "答案。" |]
        let submitArgs = createObj [ "entries", box entries ]
        let submitCtx = createObj [ "directory", box workspaceDir; "sessionID", box "mux-wiki-nojob-session" ]
        let! result = ((get submitTool "execute") $ (submitCtx, submitArgs)) |> unbox<JS.Promise<string>>
        check "mux return_bookkeeper rejects unregistered job even with directory" (result = "No active wiki job for this session.")
        let! files = readAllWikiFiles workspaceDir
        check "mux return_bookkeeper does not create files for unregistered job" (List.isEmpty files)
    do! rmAsync workspaceDir
}

let muxTopLevelPolicySpec () =
    promise {
        let managerPolicy = getPluginToolPolicy "x" (box "manager")
        let managerRemoves = unbox<string[]> (get managerPolicy "remove")
        check "mux top-level policy manager removes write" (managerRemoves |> Array.contains "write")
        check "mux top-level policy manager keeps submit_review" (not (managerRemoves |> Array.contains "submit_review"))
        check "mux top-level policy manager removes fuzzy_grep" (managerRemoves |> Array.contains "fuzzy_grep")
        let coderPolicy = getPluginToolPolicy "x" (box "coder")
        let coderRemoves = unbox<string[]> (get coderPolicy "remove")
        check "mux top-level policy coder keeps write" (not (coderRemoves |> Array.contains "write"))
        check "mux top-level policy coder removes submit_review" (coderRemoves |> Array.contains "submit_review")
        let defaultPolicy = getPluginToolPolicy "x" null
        let defaultRemoves = unbox<string[]> (get defaultPolicy "remove")
        check "mux top-level policy defaults to manager" (defaultRemoves |> Array.contains "write")
    }

let muxTopLevelDedupSpec () =
    promise {
        let mutable callIdCounter = 0
        let readMsg (content: string) : obj =
            callIdCounter <- callIdCounter + 1
            box {| parts = [| box {| ``type`` = "dynamic-tool"; toolName = "file_read"; state = "output-available"; output = box {| content = content |}; toolCallId = string callIdCounter |} |] |}
        let history = [| readMsg "seen" |]
        let window = [| readMsg "seen" |]
        let seen = collectReadOutputs history
        check "mux top-level collectReadOutputs returns content" (seen.Length = 1 && seen.[0] = "seen")
        let result = deduplicateReadOutputsWithSeen seen window
        check "mux top-level dedup returns array" (result.Length = 1)
        let output = get (unbox<obj[]> (get result.[0] "parts")).[0] "output"
        check "mux top-level dedup replaces repeat content" (str output "content" = "[No Change Since Previous Read/Write]")
    }

let muxExecutorModeSchemaSpec () = promise {
    let reg = createRegistration (createObj [])
    let modeSchema = muxExecutorModeSchema reg
    check "mux executor mode schema exists" (not (isNullish modeSchema))
    check "mux executor mode schema enum ro/rw" (enumValues modeSchema = [| "ro"; "rw" |])
    // mode is optional (not required) because Mux has no after-hook/bookkeeper infrastructure
    let executor = muxToolByName reg "executor"
    let props = get (get executor "parameters") "properties"
    check "mux executor mode is not in required" (not (isNullish (get props "mode")))
}

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
            "muxFetchWikiSnapshot", muxFetchWikiSnapshotSpec
            "muxReturnBookkeeperAppend", muxReturnBookkeeperAppendSpec
            "muxReturnBookkeeperNoActiveJob", muxReturnBookkeeperNoActiveJobSpec
            "muxExecutorModeSchema", muxExecutorModeSchemaSpec
            "muxTopLevelPolicy", muxTopLevelPolicySpec
            "muxTopLevelDedup", muxTopLevelDedupSpec
            "muxSubmitReviewNoActiveReview", muxSubmitReviewNoActiveReviewSpec
            "muxSubmitReviewPromptSuppliesCallId", muxSubmitReviewPromptSuppliesCallIdSpec
        ]
        for (label, spec) in specs do
            do! timedAsync ("IntegrationTool." + label) spec
    }
