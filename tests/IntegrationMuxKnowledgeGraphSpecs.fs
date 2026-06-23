module VibeFs.Tests.IntegrationMuxKnowledgeGraphSpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Tests.IntegrationMuxSetup

open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.Executor
open VibeFs.Mux.Plugin
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.Dyn


let muxReturnBookkeeperAppendSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-kg-submit-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-14") (DayHeader("2026-06-14", true)) [ knowledgeGraphEntry "0a3f" ["项目"; "插件入口"] "Old answer" ]
    let reg = createRegistration (createObj [])
    let submitTool = muxToolByName reg "return_bookkeeper"
    if isNullish submitTool then
        check "mux registration exposes return_bookkeeper tool" false
    else
        let runtime = muxKnowledgeGraphRuntime reg
        if isNullish runtime then
            check "mux registration exposes knowledge graph runtime for testing" false
        else
            let today = System.DateTime.UtcNow.ToString("yyyy-MM-dd")
            registerMuxKnowledgeGraphJobForTest reg "mux-kg-job" workspaceDir "append" (createObj [ "today", box today ])
            let entries = [|
                knowledgeGraphDraftEntry (Some "0a3f") ["项目"; "插件入口"] "Updated answer"
                knowledgeGraphDraftEntry None ["新知识"] "Fresh answer"
            |]
            let submitArgs = createObj [ "entries", box entries ]
            let submitCtx = createObj [ "directory", box workspaceDir; "sessionID", box "mux-kg-job" ]
            let! result = ((get submitTool "execute") $ (submitCtx, submitArgs)) |> unbox<JS.Promise<string>>
            check "mux return_bookkeeper returns a response" (result <> "")
            let! projection = readKnowledgeGraphProjectionAsync workspaceDir
            let updated = Map.find (tryParseId "0a3f" |> Option.get) projection
            check "mux return_bookkeeper updates existing id" (updated.fact = "Updated answer" && updated.entity = ["项目"; "插件入口"])
            let fresh = projection |> Map.toList |> List.tryFind (fun (_, entry) -> entry.fact = "Fresh answer")
            check "mux return_bookkeeper allocates id for new entry" (
                match fresh with
                | Some (id, entry) -> idValue id <> "" && entry.fact = "Fresh answer" && entry.entity = ["新知识"]
                | None -> false)
            let! files = readAllKnowledgeGraphFiles workspaceDir
            let dayFile = files |> List.tryFind (fun file -> let (DayHeader(date, _)) = file.header in date = today)
            check "mux return_bookkeeper creates today file" (dayFile.IsSome)
    do! rmAsync workspaceDir
}

let muxReturnBookkeeperNoActiveJobSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-kg-nojob-"
    do! ensureKnowledgeGraphDir workspaceDir
    let reg = createRegistration (createObj [])
    let submitTool = muxToolByName reg "return_bookkeeper"
    if isNullish submitTool then
        check "mux registration exposes return_bookkeeper tool" false
    else
        let entries = [| knowledgeGraphDraftEntry None ["问题"] "答案。" |]
        let submitArgs = createObj [ "entries", box entries ]
        let submitCtx = createObj [ "directory", box workspaceDir; "sessionID", box "mux-kg-nojob-session" ]
        let! result = ((get submitTool "execute") $ (submitCtx, submitArgs)) |> unbox<JS.Promise<string>>
        check "mux return_bookkeeper rejects unregistered job even with directory" (result = "No active knowledge graph job for this session.")
        let! files = readAllKnowledgeGraphFiles workspaceDir
        check "mux return_bookkeeper does not create files for unregistered job" (List.isEmpty files)
    do! rmAsync workspaceDir
}

let muxReturnBookkeeperReconstructsJobFromHistorySpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-kg-history-"
    do! ensureKnowledgeGraphDir workspaceDir
    let sessionID = "mux-kg-history-session"
    let marker = renderJobMarker { workspaceRoot = workspaceDir; kind = AppendAfterWork }
    let deps = muxDepsWithChatHistory sessionID [| box marker |]
    let reg = createRegistration deps
    let submitTool = muxToolByName reg "return_bookkeeper"
    if isNullish submitTool then
        check "mux registration exposes return_bookkeeper tool" false
    else
        let submitCtx = createObj [ "directory", box workspaceDir; "sessionID", box sessionID ]
        let submitArgs = createObj [ "entries", box [| knowledgeGraphDraftEntry None ["历史重建问题"] "历史重建答案" |] ]
        let! result = ((get submitTool "execute") $ (submitCtx, submitArgs)) |> unbox<JS.Promise<string>>
        check "mux return_bookkeeper reconstructs job from history" (result.Contains "Appended 1 knowledge graph entries")
        let! projection = readKnowledgeGraphProjectionAsync workspaceDir
        check "mux return_bookkeeper history reconstruction persists entry" (
            projection |> Map.toList |> List.exists (fun (_, entry) -> entry.entity = ["历史重建问题"] && entry.fact = "历史重建答案"))
    do! rmAsync workspaceDir
}

let muxReturnBookkeeperAppendDoesNotTriggerMaintenanceSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-kg-no-maintenance-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-18") (DayHeader("2026-06-18", false)) [
        for i in 1 .. 10 do yield knowledgeGraphEntry (sprintf "%04x" i) [sprintf "积压问题 %d" i] "Candidate" ]
    let today = System.DateTime.UtcNow.ToString("yyyy-MM-dd")
    let reg = createRegistration (minimalMuxDeps ())
    registerMuxKnowledgeGraphJobForTest reg "mux-kg-launch-job" workspaceDir "append" (createObj [ "today", box today ])
    let submitTool = muxToolByName reg "return_bookkeeper"
    if isNullish submitTool then
        check "mux registration exposes return_bookkeeper tool" false
    else
        let submitCtx = createObj [ "directory", box workspaceDir; "sessionID", box "mux-kg-launch-job" ]
        let submitArgs = createObj [ "entries", box [| knowledgeGraphDraftEntry None ["触发问题"] "触发答案" |] ]
        let! result = ((get submitTool "execute") $ (submitCtx, submitArgs)) |> unbox<JS.Promise<string>>
        check "mux return_bookkeeper append accepted" (result <> "")
        do! waitForBackgroundJobsForTesting reg
        let launches = takeBookkeeperLaunchesForTesting reg
        check "mux return_bookkeeper append does not trigger maintenance" (launches.Length = 0)
    do! rmAsync workspaceDir
}

let muxExecutorRwTriggersMaintenanceSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-executor-maintenance-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-18") (DayHeader("2026-06-18", false)) [ knowledgeGraphEntry "0a3f" ["积压问题"] "Daily candidate" ]
    let deps = minimalMuxDeps ()
    deps?("directory") <- workspaceDir
    let reg = createRegistration deps
    let executor = muxToolByName reg "executor"
    if isNullish executor then
        check "mux registration exposes executor tool" false
    else
        let ctx = createObj [ "directory", box workspaceDir; "workspaceId", box "mux-executor-maintenance"; "sessionID", box "mux-executor-maintenance" ]
        let args = createObj [ "language", box "shell"; "program", box "printf mux-maintenance"; "timeout_type", box "short"; "mode", box "rw" ]
        let! result = ((get executor "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        check "mux rw executor returns output" (result.Contains "mux-maintenance")
        let after = get reg "tool.execute.after"
        let afterInput =
            createObj
                [ "tool", box "executor"
                  "sessionID", box "mux-executor-maintenance"
                  "callID", box "mux-exec-after"
                  "args", box args
                  "directory", box workspaceDir ]
        let afterOutput = createObj [ "output", box result ]
        do! after $ (afterInput, afterOutput) |> unbox<JS.Promise<unit>>
        do! waitForBackgroundJobsForTesting reg
        let launches = takeBookkeeperLaunchesForTesting reg
        check "mux rw executor triggers maintenance" (
            launches |> Array.exists (fun launch ->
                let title = (str launch "title").ToLowerInvariant()
                let prompt = (str launch "prompt").ToLowerInvariant()
                title.Contains "daily" || prompt.Contains "daily" || title.Contains "rewrite" || prompt.Contains "rewrite"))
    do! rmAsync workspaceDir
}
