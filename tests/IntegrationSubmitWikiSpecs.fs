module VibeFs.Tests.IntegrationSubmitWikiSpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Wiki
open VibeFs.Mux.Plugin
open VibeFs.Opencode.Plugin
open VibeFs.Opencode.WikiRuntime
open VibeFs.Mux.AiSettings
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.WikiFiles

let submitWikiAppendSpec () = promise {
    let! workspaceDir = mkdtempAsync "submit-wiki-append-"
    do! ensureWikiDir workspaceDir
    do! writeWikiFileAsync (dayPath workspaceDir "2026-06-14") (DayHeader("2026-06-14", true)) [ wikiEntry "0a3f" "项目插件入口在哪里？" "Old answer" ]
    let appendDay = "2026-06-20"
    let! p = plugin (box {| directory = workspaceDir; nowMs = dayMs appendDay |})
    registerWikiJobForTest (pluginWikiRuntime p) "wiki-job-append" workspaceDir "append" (createObj [ "today", box appendDay ])
    let submitTool = submitWikiTool p
    let entries =
        [|
            wikiDraftEntry (Some "0a3f") "项目插件入口在哪里？" "Updated answer"
            wikiDraftEntry None "新知识？" "Fresh answer"
            wikiDraftEntry (Some "ffff") "未知旧 id" "Should become new id"
        |]
    let! result =
        ((get submitTool "execute")
            $ (createObj [ "entries", box entries ], createObj [ "directory", box workspaceDir; "sessionID", box "wiki-job-append" ]))
        |> unbox<JS.Promise<string>>
    check "submit_wiki append returns a response" (result <> "")

    let! projection = readProjectionAsync workspaceDir
    let updated = Map.find (tryParseId "0a3f" |> Option.get) projection
    check "submit_wiki append updates existing id" (updated.a = "Updated answer")
    let fresh = projection |> Map.toList |> List.tryFind (fun (_, entry) -> entry.q = "新知识？")
    check "submit_wiki append allocates id for new entry" (
        match fresh with
        | Some (id, entry) -> idValue id <> "" && entry.a = "Fresh answer"
        | None -> false)
    let remapped = projection |> Map.toList |> List.tryFind (fun (_, entry) -> entry.q = "未知旧 id")
    check "submit_wiki append does not keep unknown id" (
        match remapped with
        | Some (id, entry) -> idValue id <> "ffff" && entry.a = "Should become new id"
        | None -> false)

    let! files = readAllWikiFiles workspaceDir
    let dayFile = files |> List.tryFind (fun file -> let (DayHeader(date, _)) = file.header in date = appendDay)
    check "submit_wiki append creates today file" (dayFile.IsSome)
    match dayFile with
    | Some file ->
        let (DayHeader(_, rewritten)) = file.header
        check "submit_wiki append keeps day file unrewritten" (not rewritten)
    | None -> ()
    do! rmAsync workspaceDir
}

let submitWikiAppendEmptySpec () = promise {
    let! workspaceDir = mkdtempAsync "submit-wiki-empty-"
    do! ensureWikiDir workspaceDir
    let appendDay = "2026-06-20"
    let! p = plugin (box {| directory = workspaceDir; nowMs = dayMs appendDay |})
    registerWikiJobForTest (pluginWikiRuntime p) "wiki-job-empty" workspaceDir "append" (createObj [ "today", box appendDay ])
    let submitTool = submitWikiTool p
    let! result =
        ((get submitTool "execute")
            $ (createObj [ "entries", box [||] ], createObj [ "directory", box workspaceDir; "sessionID", box "wiki-job-empty" ]))
        |> unbox<JS.Promise<string>>
    check "submit_wiki empty array returns a response" (result <> "")

    let! files = readAllWikiFiles workspaceDir
    let dayFile = files |> List.tryFind (fun file -> let (DayHeader(date, _)) = file.header in date = appendDay)
    check "submit_wiki empty array does not create day file" (dayFile.IsNone)
    do! rmAsync workspaceDir
}

let submitWikiAppendDoesNotTriggerMaintenanceSpec () = promise {
    let! workspaceDir = mkdtempAsync "submit-wiki-no-maintenance-"
    do! ensureWikiDir workspaceDir
    do! writeWikiFileAsync (dayPath workspaceDir "2026-06-18") (DayHeader("2026-06-18", false)) [ wikiEntry "0a3f" "积压问题" "Daily candidate" ]
    let appendDay = "2026-06-20"
    let! p = plugin (box {| directory = workspaceDir; nowMs = dayMs appendDay |})
    registerWikiJobForTest (pluginWikiRuntime p) "wiki-job-no-maintenance" workspaceDir "append" (createObj [ "today", box appendDay ])
    let submitTool = submitWikiTool p
    let! result =
        ((get submitTool "execute")
            $ (createObj [ "entries", box [| wikiDraftEntry None "纯写入问题" "Fresh answer" |] ], createObj [ "directory", box workspaceDir; "sessionID", box "wiki-job-no-maintenance" ]))
        |> unbox<JS.Promise<string>>
    check "submit_wiki append writes entries" (result.Contains "Appended 1 wiki entries")
    do! waitForBackgroundJobsForTesting p
    let launches = takeBookkeeperLaunchesForTesting p
    check "submit_wiki append does not trigger maintenance" (launches.Length = 0)
    do! rmAsync workspaceDir
}

let submitWikiSchemaAllowsEmptySpec () = promise {
    let! workspaceDir = mkdtempAsync "submit-wiki-schema-empty-"
    do! ensureWikiDir workspaceDir
    let! p = plugin (box {| directory = workspaceDir |})
    let submitDef = submitWikiTool p
    let argsSchema = get submitDef "args"
    let entriesSchema =
        let direct = get argsSchema "entries"
        if not (isNullish direct) then direct
        else
            let shape = get argsSchema "shape"
            if not (isNullish shape) then get shape "entries" else null
    check "submit_wiki entries schema is exposed" (not (isNullish entriesSchema))
    let empty : obj = box (Array.empty<obj>)
    let parsed = entriesSchema?safeParse(empty)
    let success = unbox<bool> (get parsed "success")
    check "submit_wiki entries schema accepts empty array" success
    let filled : obj = box [| wikiDraftEntry None "q" "a" |]
    let parsedFilled = entriesSchema?safeParse(filled)
    let successFilled = unbox<bool> (get parsedFilled "success")
    check "submit_wiki entries schema still accepts a valid entry" successFilled
    do! rmAsync workspaceDir
}

let submitWikiDailyRewriteSpec () = promise {
    let! workspaceDir = mkdtempAsync "submit-wiki-daily-"
    do! ensureWikiDir workspaceDir
    let dayFilePath = dayPath workspaceDir "2026-06-18"
    let dayFileContent =
        renderNdjson (DayHeader("2026-06-18", false)) [
            wikiEntry "0a3f" "项目插件入口在哪里？" "Old answer"
            wikiEntry "b912" "Magic Todo backlog 如何保存？" "Old backlog answer"
        ]
    do! writeFileAsync dayFilePath dayFileContent
    let! p = plugin (box {| directory = workspaceDir |})
    registerWikiJobForTest (pluginWikiRuntime p) "wiki-job-day" workspaceDir "daily" (createObj [ "date", box "2026-06-18" ])
    let submitTool = submitWikiTool p
    let! result =
        ((get submitTool "execute")
            $ (createObj [ "entries", box [| wikiDraftEntry None "合并后问题" "Canonical answer" |] ], createObj [ "directory", box workspaceDir; "sessionID", box "wiki-job-day" ]))
        |> unbox<JS.Promise<string>>
    check "submit_wiki daily rewrite returns a response" (result <> "")

    let! files = readAllWikiFiles workspaceDir
    let dayFile = files |> List.find (fun file -> let (DayHeader(date, _)) = file.header in date = "2026-06-18")
    let (DayHeader(date, rewritten)) = dayFile.header
    check "submit_wiki daily rewrite flips rewritten header" (date = "2026-06-18" && rewritten)
    check "submit_wiki daily rewrite replaces entries" (dayFile.entries.Length = 1 && dayFile.entries.Head.q = "合并后问题" && dayFile.entries.Head.a = "Canonical answer")
    do! rmAsync workspaceDir
}

let submitWikiReconstructsJobFromHistorySpec () = promise {
    let! workspaceDir = mkdtempAsync "wiki-job-history-"
    do! ensureWikiDir workspaceDir
    let sessionID = "wiki-history-session"
    let marker = renderJobMarker { workspaceRoot = workspaceDir; kind = AppendAfterWork }
    let mockClient =
        createObj [ "session", box (createObj [
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (promise { return box {| data = [| userTextMessage sessionID marker |] |} })))
        ]) ]
    let! p = plugin (box {| directory = workspaceDir; client = mockClient; nowMs = dayMs "2026-06-20" |})
    let submitTool = submitWikiTool p
    let! result =
        ((get submitTool "execute")
            $ (createObj [ "entries", box [| wikiDraftEntry None "历史重建问题" "历史重建答案" |] ], createObj [ "directory", box workspaceDir; "sessionID", box sessionID ]))
        |> unbox<JS.Promise<string>>
    check "submit_wiki reconstructs job from history" (result.Contains "Appended 1 wiki entries")
    let! projection = readProjectionAsync workspaceDir
    check "submit_wiki history reconstruction persists entry" (projection |> Map.toList |> List.exists (fun (_, entry) -> entry.q = "历史重建问题" && entry.a = "历史重建答案"))
    do! rmAsync workspaceDir
}
