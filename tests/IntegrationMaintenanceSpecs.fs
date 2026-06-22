module VibeFs.Tests.IntegrationMaintenanceSpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Message
open VibeFs.Kernel.Wiki
open VibeFs.Mux.Plugin
open VibeFs.Opencode.Plugin
open VibeFs.Opencode.WikiRuntime
open VibeFs.Mux.AiSettings
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.WikiFiles

let dailyMaintenanceLaunchSpec () = promise {
    let! workspaceDir = mkdtempAsync "daily-maintenance-"
    do! ensureWikiDir workspaceDir
    let dayFilePath = dayPath workspaceDir "2026-06-18"
    do! writeWikiFileAsync dayFilePath (DayHeader("2026-06-18", false)) [ wikiEntry "0a3f" "项目插件入口在哪里？" "Daily candidate" ]
    let mockClient = bookkeeperMockClient [| assistantCompletionMessage "daily-session" "Wiki prelude" |]
    let! pluginObject = plugin (box {| directory = workspaceDir; client = mockClient; nowMs = dayMs "2026-06-20" |})
    let wikiRuntime = get (pluginWikiRuntime pluginObject) "rawInstance" :?> WikiRuntime
    do! wikiRuntime.StartMaintenanceIfDue(workspaceDir)

    let launches = takeBookkeeperLaunchesForTesting pluginObject
    check "daily maintenance schedules one launch" (launches.Length = 1)
    check "daily maintenance launch mentions daily rewrite" (
        let title = str launches.[0] "title"
        let prompt = str launches.[0] "prompt"
        title.ToLowerInvariant().Contains "daily" || prompt.ToLowerInvariant().Contains "daily" || prompt.ToLowerInvariant().Contains "rewrite")
    do! waitForBackgroundJobsForTesting pluginObject
    do! rmAsync workspaceDir
}

let weeklyMaintenanceLaunchSpec () = promise {
    let! workspaceDir = mkdtempAsync "weekly-maintenance-"
    do! ensureWikiDir workspaceDir
    let snapshotFilePath = snapshotPath workspaceDir
    do! writeWikiFileAsync snapshotFilePath (SnapshotHeader(Some "2026-06-13")) [ wikiEntry "0a3f" "项目插件入口在哪里？" "Snapshot baseline" ]
    do! writeWikiFileAsync (dayPath workspaceDir "2026-06-14") (DayHeader("2026-06-14", true)) [ wikiEntry "b912" "Magic Todo backlog 如何保存？" "Weekly candidate one" ]
    let mockClient = bookkeeperMockClient [| assistantCompletionMessage "weekly-session" "Wiki prelude" |]
    let! pluginObject = plugin (box {| directory = workspaceDir; client = mockClient; nowMs = dayMs "2026-06-15" |})
    let wikiRuntime = get (pluginWikiRuntime pluginObject) "rawInstance" :?> WikiRuntime
    do! wikiRuntime.StartMaintenanceIfDue(workspaceDir)

    let launches = takeBookkeeperLaunchesForTesting pluginObject
    check "weekly maintenance schedules at least one launch" (launches.Length >= 1)
    check "weekly maintenance launch mentions snapshot or weekly" (
        launches
        |> Array.exists (fun launch ->
            let title = (str launch "title").ToLowerInvariant()
            let prompt = (str launch "prompt").ToLowerInvariant()
            title.Contains "snapshot" || title.Contains "weekly" || prompt.Contains "snapshot" || prompt.Contains "weekly"))
    do! waitForBackgroundJobsForTesting pluginObject
    do! rmAsync workspaceDir
}

let weeklyMaintenanceUsesLastSundaySpec () = promise {
    let! workspaceDir = mkdtempAsync "weekly-maintenance-sunday-"
    do! ensureWikiDir workspaceDir
    let snapshotFilePath = snapshotPath workspaceDir
    let snapshotThrough = "2026-06-07"
    do! writeWikiFileAsync snapshotFilePath (SnapshotHeader(Some snapshotThrough)) [ wikiEntry "0a3f" "项目插件入口在哪里？" "Snapshot baseline" ]
    for day in [ "2026-06-08"; "2026-06-09"; "2026-06-10"; "2026-06-11"; "2026-06-12"; "2026-06-13"; "2026-06-14" ] do
        do! writeWikiFileAsync (dayPath workspaceDir day) (DayHeader(day, true)) [ wikiEntry "b912" ("周内问题 " + day) "Day entry" ]
    let lastSunday = "2026-06-14"
    let mockClient = bookkeeperMockClient [| assistantCompletionMessage "weekly-sunday-session" "Wiki prelude" |]
    let! pluginObject = plugin (box {| directory = workspaceDir; client = mockClient; nowMs = dayMs "2026-06-15" |})
    let wikiRuntime = get (pluginWikiRuntime pluginObject) "rawInstance" :?> WikiRuntime
    do! wikiRuntime.StartMaintenanceIfDue(workspaceDir)

    do! waitForBackgroundJobsForTesting pluginObject
    let launches = takeBookkeeperLaunchesForTesting pluginObject
    check "weekly maintenance lastSunday schedules exactly one weekly launch" (launches.Length = 1)
    let launch = launches.[0]
    let title = str launch "title"
    let prompt = str launch "prompt"
    check "weekly maintenance launch references lastSunday cutoff" (title.Contains lastSunday || prompt.Contains lastSunday)
    check "weekly maintenance launch does not reference old snapshot through" (not (title.Contains snapshotThrough) && not (prompt.Contains snapshotThrough))
    do! rmAsync workspaceDir
}

let weeklyMaintenanceWithoutSnapshotFileSpec () = promise {
    let! workspaceDir = mkdtempAsync "weekly-maintenance-no-snapshot-"
    do! ensureWikiDir workspaceDir
    do! ensureTodayFile workspaceDir "2026-06-15"
    do! rewriteDay workspaceDir "2026-06-10" [ wikiEntry "0a3f" "周初问题" "Day 10 entry" ]
    do! rewriteDay workspaceDir "2026-06-12" [ wikiEntry "b912" "周中问题" "Day 12 entry" ]
    let mockClient = bookkeeperMockClient [| assistantCompletionMessage "weekly-no-snapshot-session" "Wiki prelude" |]
    let! pluginObject = plugin (box {| directory = workspaceDir; client = mockClient; nowMs = dayMs "2026-06-15" |})
    let wikiRuntime = get (pluginWikiRuntime pluginObject) "rawInstance" :?> WikiRuntime
    do! wikiRuntime.StartMaintenanceIfDue(workspaceDir)

    let launches = takeBookkeeperLaunchesForTesting pluginObject
    check "weekly maintenance without snapshot file schedules at least one launch" (launches.Length >= 1)
    check "weekly maintenance without snapshot file mentions snapshot or weekly" (
        launches
        |> Array.exists (fun launch ->
            let title = (str launch "title").ToLowerInvariant()
            let prompt = (str launch "prompt").ToLowerInvariant()
            title.Contains "snapshot" || title.Contains "weekly" || prompt.Contains "snapshot" || prompt.Contains "weekly"))
    do! waitForBackgroundJobsForTesting pluginObject
    do! rmAsync workspaceDir
}

let heartbeatTriggersMaintenanceSpec () = promise {
    let! workspaceDir = mkdtempAsync "heartbeat-maintenance-"
    do! ensureWikiDir workspaceDir
    let dayFilePath = dayPath workspaceDir "2026-06-18"
    do! writeWikiFileAsync dayFilePath (DayHeader("2026-06-18", false)) [ wikiEntry "0a3f" "积压问题" "Daily candidate" ]
    let mockClient = bookkeeperMockClient [| assistantCompletionMessage "heartbeat-session" "Wiki prelude" |]
    let! p = plugin (box {| directory = workspaceDir; client = mockClient; nowMs = dayMs "2026-06-20" |})
    let wikiRuntime = get (pluginWikiRuntime p) "rawInstance" :?> WikiRuntime
    wikiRuntime.StartBookkeeperAppend("input", "result", "write", parentSessionID = "heartbeat-parent")
    do! waitForBackgroundJobsForTesting p
    let launches = takeBookkeeperLaunchesForTesting p
    check "heartbeat: maintenance launch triggered on append heartbeat" (
        launches |> Array.exists (fun l ->
            let title = (str l "title").ToLowerInvariant()
            let prompt = (str l "prompt").ToLowerInvariant()
            title.Contains "daily" || title.Contains "rewrite" || prompt.Contains "daily" || prompt.Contains "rewrite"))
    do! rmAsync workspaceDir
}

let heartbeatMaintenanceUsesParentSessionSpec () = promise {
    let! workspaceDir = mkdtempAsync "heartbeat-maintenance-parent-"
    do! ensureWikiDir workspaceDir
    do! writeWikiFileAsync (dayPath workspaceDir "2026-06-18") (DayHeader("2026-06-18", false)) [ wikiEntry "0a3f" "积压问题" "Daily candidate" ]

    let createCalls = ResizeArray<obj>()
    let mockClient =
        createObj [ "session", box (createObj [
            "create", box (System.Func<obj, JS.Promise<obj>>(fun arg ->
                promise {
                    createCalls.Add(arg)
                    return box {| data = box {| id = "child-bookkeeper-session" |} |}
                }))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ()))
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return box {| data = [| assistantCompletionMessage "child-bookkeeper-session" "done" |] |} }))
            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ()))
        ]) ]

    let! p = plugin (box {| directory = workspaceDir; client = mockClient; nowMs = dayMs "2026-06-20" |})
    let wikiRuntime = get (pluginWikiRuntime p) "rawInstance" :?> WikiRuntime
    wikiRuntime.StartBookkeeperAppend("input", "result", "write", parentSessionID = "heartbeat-parent")
    do! waitForBackgroundJobsForTesting p

    let parentIds = createCalls |> Seq.map (fun call -> str (get call "body") "parentID") |> Seq.toArray
    check "heartbeat maintenance: append launch uses parent session" (parentIds |> Array.contains "heartbeat-parent")
    check "heartbeat maintenance: maintenance launch also uses parent session" (parentIds.Length >= 2 && parentIds |> Array.forall ((=) "heartbeat-parent"))
    do! rmAsync workspaceDir
}
