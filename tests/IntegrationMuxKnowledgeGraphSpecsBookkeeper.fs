module VibeFs.Tests.IntegrationMuxKnowledgeGraphSpecsBookkeeper

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Tests.IntegrationMuxSetup

open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraph.Types
open VibeFs.Kernel.Messaging
open VibeFs.Kernel.KnowledgeGraph.Prompts
open VibeFs.Mux.Plugin
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.Dyn


let muxReturnBookkeeperAppendSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-kg-submit-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-14") (DayHeader("2026-06-14", true)) [ knowledgeGraphEntry "0a3f" ["项目"; "插件入口"] "Old answer" ]
    let reg = sharedMuxRegistration ()
    let submitTool = muxToolByName reg "return_bookkeeper"
    if isNullish submitTool then
        check "mux registration exposes return_bookkeeper tool" false
    else
        let runtime = muxKnowledgeGraphRuntime reg
        if isNullish runtime then
            check "mux registration exposes knowledge graph runtime for testing" false
        else
            let today = integrationKnowledgeGraphToday
            registerMuxKnowledgeGraphJobForTest reg "mux-kg-job" workspaceDir "append" (createObj [ "today", box today ])
            let entries = [|
                knowledgeGraphDraftEntry (Some "0a3f") ["项目"; "插件入口"] "Updated answer"
                knowledgeGraphDraftEntry None ["新知识"] "Fresh answer"
            |]
            let submitArgs = createObj [ "entries", box entries ]
            let submitCtx = muxToolConfig workspaceDir "mux-kg-job"
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
    let reg = sharedMuxRegistration ()
    let submitTool = muxToolByName reg "return_bookkeeper"
    if isNullish submitTool then
        check "mux registration exposes return_bookkeeper tool" false
    else
        let entries = [| knowledgeGraphDraftEntry None ["问题"] "答案。" |]
        let submitArgs = createObj [ "entries", box entries ]
        let submitCtx = muxToolConfig workspaceDir "mux-kg-nojob-session"
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
        let submitCtx = muxToolConfig workspaceDir sessionID
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
    let today = integrationKnowledgeGraphToday
    let reg = sharedMuxRegistration ()
    registerMuxKnowledgeGraphJobForTest reg "mux-kg-launch-job" workspaceDir "append" (createObj [ "today", box today ])
    let submitTool = muxToolByName reg "return_bookkeeper"
    if isNullish submitTool then
        check "mux registration exposes return_bookkeeper tool" false
    else
        let submitCtx = muxToolConfig workspaceDir "mux-kg-launch-job"
        let submitArgs = createObj [ "entries", box [| knowledgeGraphDraftEntry None ["触发问题"] "触发答案" |] ]
        let! result = ((get submitTool "execute") $ (submitCtx, submitArgs)) |> unbox<JS.Promise<string>>
        check "mux return_bookkeeper append accepted" (result <> "")
        do! waitForBackgroundJobsForTesting reg
        let launches = takeBookkeeperLaunchesForTesting reg
        check "mux return_bookkeeper append does not trigger maintenance" (launches.Length = 0)
    do! rmAsync workspaceDir
}

let muxReturnBookkeeperRejectsSecondCallSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-kg-second-"
    do! ensureKnowledgeGraphDir workspaceDir
    let sessionID = "mux-kg-second-session"
    let messages = ResizeArray<obj>()
    let deps = muxMutableDepsWithChatHistory sessionID messages
    let reg = createRegistration deps
    let submitTool = muxToolByName reg "return_bookkeeper"
    if isNullish submitTool then
        check "mux registration exposes return_bookkeeper tool for second call" false
    else
        let today = integrationKnowledgeGraphToday
        registerMuxKnowledgeGraphJobForTest reg sessionID workspaceDir "append" (createObj [ "today", box today ])

        // First submit: should succeed.
        let entries1 = [| knowledgeGraphDraftEntry None ["首次问题"] "首次答案" |]
        let args1 = createObj [ "entries", box entries1 ]
        let ctx1 = muxToolConfig workspaceDir sessionID
        let! result1 = ((get submitTool "execute") $ (ctx1, args1)) |> unbox<JS.Promise<string>>
        check "mux first return_bookkeeper succeeds" (result1.Contains "Appended 1 knowledge graph entries")

        let! filesAfterFirst = readAllKnowledgeGraphFiles workspaceDir
        let entryCountAfterFirst =
            filesAfterFirst |> List.sumBy (fun f -> f.entries.Length)

        // Simulate chat history now containing marker + completed return_bookkeeper tool.
        let marker = renderJobMarker { workspaceRoot = workspaceDir; kind = AppendAfterWork }
        messages.Add(box {| id = sessionID + "-marker"; role = "user"; parts = [| box {| ``type`` = "text"; text = marker |} |] |})
        messages.Add(box
            {| id = sessionID + "-tool"
               role = "assistant"
               parts =
                [| box
                    {| ``type`` = "dynamic-tool"
                       toolName = "return_bookkeeper"
                       toolCallId = "mux-kg-second-call"
                       state = "output-available"
                       input = createObj [ "entries", box entries1 ]
                       output = result1 |} |] |})

        // Second submit: should return scold, not append.
        let entries2 = [| knowledgeGraphDraftEntry None ["二次问题"] "二次答案" |]
        let args2 = createObj [ "entries", box entries2 ]
        let ctx2 = muxToolConfig workspaceDir sessionID
        let! result2 = ((get submitTool "execute") $ (ctx2, args2)) |> unbox<JS.Promise<string>>

        check "mux second return_bookkeeper returns scold not append success"
            (not (result2.Contains "Appended") && result2 <> "")
        check "mux second return_bookkeeper contains rejection phrase"
            (result2.Contains "already completed" || result2.Contains "Do not call return_bookkeeper again")
        check "mux second return_bookkeeper does not mention No active job"
            (not (result2.Contains "No active knowledge graph job"))

        let! filesAfterSecond = readAllKnowledgeGraphFiles workspaceDir
        let entryCountAfterSecond =
            filesAfterSecond |> List.sumBy (fun f -> f.entries.Length)
        check "mux second return_bookkeeper no extra NDJSON entries"
            (entryCountAfterSecond = entryCountAfterFirst)

    do! rmAsync workspaceDir
}
