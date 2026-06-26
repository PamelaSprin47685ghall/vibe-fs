module Wanxiangshu.Tests.IntegrationSubmitKnowledgeGraphSpecsAppend

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Kernel.KnowledgeGraph
open Wanxiangshu.Kernel.KnowledgeGraph.Types
open Wanxiangshu.Kernel.KnowledgeGraph.Codec
open Wanxiangshu.Opencode.Plugin
open Wanxiangshu.Opencode.KnowledgeGraphRuntime
open Wanxiangshu.Shell.KnowledgeGraphFiles
open Wanxiangshu.Shell.Dyn

let submitKnowledgeGraphAppendSpec () = promise {
    let! workspaceDir = mkdtempAsync "submit-kg-append-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-14") (DayHeader("2026-06-14", true)) [ knowledgeGraphEntry "0a3f" ["项目"; "插件入口"] "Old answer" ]
    let appendDay = "2026-06-20"
    let! p = plugin (box {| directory = workspaceDir; nowMs = dayMs appendDay |})
    registerKnowledgeGraphJobForTest (pluginKnowledgeGraphRuntime p) "kg-job-append" workspaceDir "append" (createObj [ "today", box appendDay ])
    let submitTool = submitKnowledgeGraphTool p
    let entries =
        [|
            knowledgeGraphDraftEntry (Some "0a3f") ["项目"; "插件入口"] "Updated answer"
            knowledgeGraphDraftEntry None ["新知识"] "Fresh answer"
            knowledgeGraphDraftEntry (Some "ffff") ["未知旧 id"] "Should become new id"
        |]
    let! result =
        ((get submitTool "execute")
            $ (createObj [ "entries", box entries ], createObj [ "directory", box workspaceDir; "sessionID", box "kg-job-append" ]))
        |> unbox<JS.Promise<string>>
    check "return_bookkeeper append returns a response" (result <> "")
    let! projection = readKnowledgeGraphProjectionAsync workspaceDir
    let updated = Map.find (tryParseId "0a3f" |> Option.get) projection
    check "return_bookkeeper append updates existing id" (updated.fact = "Updated answer" && updated.entity = ["项目"; "插件入口"])
    let fresh = projection |> Map.toList |> List.tryFind (fun (_, entry) -> entry.fact = "Fresh answer")
    check "return_bookkeeper append allocates id for new entry" (
        match fresh with
        | Some (id, entry) -> idValue id <> "" && entry.fact = "Fresh answer" && entry.entity = ["新知识"]
        | None -> false)
    let remapped = projection |> Map.toList |> List.tryFind (fun (_, entry) -> entry.fact = "Should become new id")
    check "return_bookkeeper append does not keep unknown id" (
        match remapped with
        | Some (id, entry) -> idValue id <> "ffff" && entry.fact = "Should become new id" && entry.entity = ["未知旧 id"]
        | None -> false)
    let! files = readAllKnowledgeGraphFiles workspaceDir
    let dayFile = files |> List.tryFind (fun file -> let (DayHeader(date, _)) = file.header in date = appendDay)
    check "return_bookkeeper append creates today file" (dayFile.IsSome)
    match dayFile with
    | Some file ->
        let (DayHeader(_, rewritten)) = file.header
        check "return_bookkeeper append keeps day file unrewritten" (not rewritten)
    | None -> ()
    do! rmAsync workspaceDir
}

let submitKnowledgeGraphAppendEmptySpec () = promise {
    let! workspaceDir = mkdtempAsync "submit-kg-empty-"
    do! ensureKnowledgeGraphDir workspaceDir
    let appendDay = "2026-06-20"
    let! p = plugin (box {| directory = workspaceDir; nowMs = dayMs appendDay |})
    registerKnowledgeGraphJobForTest (pluginKnowledgeGraphRuntime p) "kg-job-empty" workspaceDir "append" (createObj [ "today", box appendDay ])
    let submitTool = submitKnowledgeGraphTool p
    let! result =
        ((get submitTool "execute")
            $ (createObj [ "entries", box [||] ], createObj [ "directory", box workspaceDir; "sessionID", box "kg-job-empty" ]))
        |> unbox<JS.Promise<string>>
    check "return_bookkeeper empty array returns a response" (result <> "")
    let! files = readAllKnowledgeGraphFiles workspaceDir
    let dayFile = files |> List.tryFind (fun file -> let (DayHeader(date, _)) = file.header in date = appendDay)
    check "return_bookkeeper empty array does not create day file" (dayFile.IsNone)
    do! rmAsync workspaceDir
}

let submitKnowledgeGraphAppendDoesNotTriggerMaintenanceSpec () = promise {
    let! workspaceDir = mkdtempAsync "submit-kg-no-maintenance-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-18") (DayHeader("2026-06-18", false)) [ knowledgeGraphEntry "0a3f" ["积压问题"] "Daily candidate" ]
    let appendDay = "2026-06-20"
    let! p = plugin (box {| directory = workspaceDir; nowMs = dayMs appendDay |})
    registerKnowledgeGraphJobForTest (pluginKnowledgeGraphRuntime p) "kg-job-no-maintenance" workspaceDir "append" (createObj [ "today", box appendDay ])
    let submitTool = submitKnowledgeGraphTool p
    let! result =
        ((get submitTool "execute")
            $ (createObj [ "entries", box [| knowledgeGraphDraftEntry None ["纯写入问题"] "Fresh answer" |] ], createObj [ "directory", box workspaceDir; "sessionID", box "kg-job-no-maintenance" ]))
        |> unbox<JS.Promise<string>>
    check "return_bookkeeper append writes entries" (result.Contains "Appended 1 knowledge graph entries")
    do! waitForBackgroundJobsForTesting p
    let launches = takeBookkeeperLaunchesForTesting p
    check "return_bookkeeper append does not trigger maintenance" (launches.Length = 0)
    do! rmAsync workspaceDir
}

let submitKnowledgeGraphSchemaAllowsEmptySpec () = promise {
    let! workspaceDir = mkdtempAsync "submit-kg-schema-empty-"
    do! ensureKnowledgeGraphDir workspaceDir
    let! p = plugin (box {| directory = workspaceDir |})
    let submitDef = submitKnowledgeGraphTool p
    let argsSchema = get submitDef "args"
    let entriesSchema =
        let direct = get argsSchema "entries"
        if not (isNullish direct) then direct
        else
            let shape = get argsSchema "shape"
            if not (isNullish shape) then get shape "entries" else null
    check "return_bookkeeper entries schema is exposed" (not (isNullish entriesSchema))
    let empty : obj = box (Array.empty<obj>)
    let parsed = entriesSchema?safeParse(empty)
    let success = unbox<bool> (get parsed "success")
    check "return_bookkeeper entries schema accepts empty array" success
    let filled : obj = box [| knowledgeGraphDraftEntry None ["q"] "a" |]
    let parsedFilled = entriesSchema?safeParse(filled)
    let successFilled = unbox<bool> (get parsedFilled "success")
    check "return_bookkeeper entries schema still accepts a valid entry" successFilled
    do! rmAsync workspaceDir
}

let submitKnowledgeGraphDailyRewriteSpec () = promise {
    let! workspaceDir = mkdtempAsync "submit-kg-daily-"
    do! ensureKnowledgeGraphDir workspaceDir
    let dayFilePath = dayPath workspaceDir "2026-06-18"
    let dayFileContent =
        renderNdjson (DayHeader("2026-06-18", false)) [
            knowledgeGraphEntry "0a3f" ["项目"; "插件入口"] "Old answer"
            knowledgeGraphEntry "b912" ["Magic Todo"; "backlog"] "Old backlog answer"
        ]
    do! writeFileAsync dayFilePath dayFileContent
    let! p = plugin (box {| directory = workspaceDir |})
    registerKnowledgeGraphJobForTest (pluginKnowledgeGraphRuntime p) "kg-job-day" workspaceDir "daily" (createObj [ "date", box "2026-06-18" ])
    let submitTool = submitKnowledgeGraphTool p
    let! result =
        ((get submitTool "execute")
            $ (createObj [ "entries", box [| knowledgeGraphDraftEntry None ["合并后问题"] "Canonical answer" |] ], createObj [ "directory", box workspaceDir; "sessionID", box "kg-job-day" ]))
        |> unbox<JS.Promise<string>>
    check "return_bookkeeper daily rewrite returns a response" (result <> "")
    let! files = readAllKnowledgeGraphFiles workspaceDir
    let dayFile = files |> List.find (fun file -> let (DayHeader(date, _)) = file.header in date = "2026-06-18")
    let (DayHeader(date, rewritten)) = dayFile.header
    check "return_bookkeeper daily rewrite flips rewritten header" (date = "2026-06-18" && rewritten)
    check "return_bookkeeper daily rewrite replaces entries" (dayFile.entries.Length = 1 && dayFile.entries.Head.entity = ["合并后问题"] && dayFile.entries.Head.fact = "Canonical answer")
    do! rmAsync workspaceDir
}

let submitKnowledgeGraphReconstructsJobFromHistorySpec () = promise {
    let! workspaceDir = mkdtempAsync "kg-job-history-"
    do! ensureKnowledgeGraphDir workspaceDir
    let sessionID = "kg-history-session"
    let marker = renderJobMarker { workspaceRoot = workspaceDir; kind = AppendAfterWork }
    let mockClient =
        createObj [ "session", box (createObj [
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (promise { return box {| data = [| userTextMessage sessionID marker |] |} })))
        ]) ]
    let! p = plugin (box {| directory = workspaceDir; client = mockClient; nowMs = dayMs "2026-06-20" |})
    let submitTool = submitKnowledgeGraphTool p
    let! result =
        ((get submitTool "execute")
            $ (createObj [ "entries", box [| knowledgeGraphDraftEntry None ["历史重建问题"] "历史重建答案" |] ], createObj [ "directory", box workspaceDir; "sessionID", box sessionID ]))
        |> unbox<JS.Promise<string>>
    check "return_bookkeeper reconstructs job from history" (result.Contains "Appended 1 knowledge graph entries")
    let! projection = readKnowledgeGraphProjectionAsync workspaceDir
    check "return_bookkeeper history reconstruction persists entry" (projection |> Map.toList |> List.exists (fun (_, entry) -> entry.entity = ["历史重建问题"] && entry.fact = "历史重建答案"))
    do! rmAsync workspaceDir
}