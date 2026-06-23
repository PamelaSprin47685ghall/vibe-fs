module VibeFs.Tests.IntegrationMaintenanceSpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup

open VibeFs.Kernel.Message
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Mux.Plugin
open VibeFs.Opencode.Plugin
open VibeFs.Opencode.KnowledgeGraphRuntime
open VibeFs.Mux.AiSettings
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.Dyn

let dailyMaintenanceLaunchSpec () = promise {
    let! workspaceDir = mkdtempAsync "daily-maintenance-"
    do! ensureKnowledgeGraphDir workspaceDir
    let dayFilePath = dayPath workspaceDir "2026-06-18"
    do! writeKnowledgeGraphFileAsync dayFilePath (DayHeader("2026-06-18", false)) [ knowledgeGraphEntry "0a3f" ["项目插件入口在哪里？"] "Daily candidate" ]
    let mockClient = bookkeeperMockClient [| assistantCompletionMessage "daily-session" "KnowledgeGraph prelude" |]
    let! pluginObject = plugin (box {| directory = workspaceDir; client = mockClient; nowMs = dayMs "2026-06-20" |})
    let knowledgeGraphRuntime = get (pluginKnowledgeGraphRuntime pluginObject) "rawInstance" :?> KnowledgeGraphRuntime
    do! knowledgeGraphRuntime.StartMaintenanceIfDue(workspaceDir)

    let launches = takeBookkeeperLaunchesForTesting pluginObject
    check "daily maintenance schedules one launch" (launches.Length = 1)
    check "daily maintenance launch mentions daily rewrite" (
        let title = str launches.[0] "title"
        let prompt = str launches.[0] "prompt"
        title.ToLowerInvariant().Contains "daily" || prompt.ToLowerInvariant().Contains "daily" || prompt.ToLowerInvariant().Contains "rewrite")
    do! waitForBackgroundJobsForTesting pluginObject
    do! rmAsync workspaceDir
}

let heartbeatTriggersMaintenanceSpec () = promise {
    let! workspaceDir = mkdtempAsync "heartbeat-maintenance-"
    do! ensureKnowledgeGraphDir workspaceDir
    let dayFilePath = dayPath workspaceDir "2026-06-18"
    do! writeKnowledgeGraphFileAsync dayFilePath (DayHeader("2026-06-18", false)) [ knowledgeGraphEntry "0a3f" ["积压问题"] "Daily candidate" ]
    let mockClient = bookkeeperMockClient [| assistantCompletionMessage "heartbeat-session" "KnowledgeGraph prelude" |]
    let! p = plugin (box {| directory = workspaceDir; client = mockClient; nowMs = dayMs "2026-06-20" |})
    let knowledgeGraphRuntime = get (pluginKnowledgeGraphRuntime p) "rawInstance" :?> KnowledgeGraphRuntime
    knowledgeGraphRuntime.StartBookkeeperAppend("input", "result", "write", parentSessionID = "heartbeat-parent")
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
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-18") (DayHeader("2026-06-18", false)) [ knowledgeGraphEntry "0a3f" ["积压问题"] "Daily candidate" ]

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
    let knowledgeGraphRuntime = get (pluginKnowledgeGraphRuntime p) "rawInstance" :?> KnowledgeGraphRuntime
    knowledgeGraphRuntime.StartBookkeeperAppend("input", "result", "write", parentSessionID = "heartbeat-parent")
    do! waitForBackgroundJobsForTesting p

    let parentIds = createCalls |> Seq.map (fun call -> str (get call "body") "parentID") |> Seq.toArray
    check "heartbeat maintenance: append launch uses parent session" (parentIds |> Array.contains "heartbeat-parent")
    check "heartbeat maintenance: maintenance launch also uses parent session" (parentIds.Length >= 2 && parentIds |> Array.forall ((=) "heartbeat-parent"))
    do! rmAsync workspaceDir
}

let heartbeatSchedulesOnlyEarliestDailyWhileAppendRunsSpec () = promise {
    let! workspaceDir = mkdtempAsync "heartbeat-maintenance-concurrent-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-18") (DayHeader("2026-06-18", false)) [ knowledgeGraphEntry "0a3f" ["第一日"] "Daily candidate 1" ]
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-19") (DayHeader("2026-06-19", false)) [ knowledgeGraphEntry "0a40" ["第二日"] "Daily candidate 2" ]

    let createCalls = ResizeArray<obj>()
    let promptCalls = ResizeArray<obj>()
    let releasePrompt = ref (fun () -> ())
    let promptGate : JS.Promise<unit> = Promise.create (fun resolve _ -> releasePrompt.Value <- resolve)
    let mockClient =
        createObj [ "session", box (createObj [
            "create", box (System.Func<obj, JS.Promise<obj>>(fun arg ->
                promise {
                    createCalls.Add(arg)
                    return box {| data = box {| id = "child-bookkeeper-session-" + string createCalls.Count |} |}
                }))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                promptCalls.Add(arg)
                promptGate))
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return box {| data = [||] |} }))
            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ()))
        ]) ]

    let! p = plugin (box {| directory = workspaceDir; client = mockClient; nowMs = dayMs "2026-06-20" |})
    let knowledgeGraphRuntime = get (pluginKnowledgeGraphRuntime p) "rawInstance" :?> KnowledgeGraphRuntime
    knowledgeGraphRuntime.StartBookkeeperAppend("input", "result", "write", parentSessionID = "heartbeat-parent")
    let! _ = knowledgeGraphRuntime.EnsureSessionSnapshot("drain-command-queue", workspaceDir)
    do! Promise.sleep 0

    check "heartbeat maintenance: append and earliest daily launch before prompt release" (createCalls.Count = 2)
    check "heartbeat maintenance: blocked prompt has both append and daily prompts" (promptCalls.Count = 2)
    let promptTexts =
        promptCalls
        |> Seq.map (fun call ->
            let parts = get (get call "body") "parts"
            if isArray parts then str (unbox<obj[]> parts).[0] "text" else "")
        |> Seq.toArray
    check "heartbeat maintenance: schedules earliest daily" (promptTexts |> Array.exists (fun text -> text.Contains "2026-06-18"))
    check "heartbeat maintenance: does not schedule later daily yet" (promptTexts |> Array.forall (fun text -> not (text.Contains "2026-06-19")))

    releasePrompt.Value()
    do! waitForBackgroundJobsForTesting p
    do! rmAsync workspaceDir
}

let dailyRewriteTriggersNextDailySpec () = promise {
    let! workspaceDir = mkdtempAsync "daily-chain-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-08") (DayHeader("2026-06-08", false)) [ knowledgeGraphEntry "b912" ["第八日问题"] "Day 8 candidate" ]
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-09") (DayHeader("2026-06-09", false)) [ knowledgeGraphEntry "c813" ["第九日问题"] "Day 9 candidate" ]

    let createCalls = ResizeArray<obj>()
    let childIds = ResizeArray<string>()
    let childPrompts = System.Collections.Generic.Dictionary<string, string>()
    let mockClient =
        createObj [ "session", box (createObj [
            "create", box (System.Func<obj, JS.Promise<obj>>(fun arg ->
                promise {
                    createCalls.Add(arg)
                    let childId = $"child-bookkeeper-session-{createCalls.Count}"
                    childIds.Add(childId)
                    return box {| data = box {| id = childId |} |}
                }))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                promise {
                    let childId = str (get arg "path") "id"
                    let parts = unbox<obj array> (get (get arg "body") "parts")
                    childPrompts.[childId] <- str parts.[0] "text"
                }))
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun arg ->
                promise {
                    let childId = str (get arg "path") "id"
                    match childPrompts.TryGetValue childId with
                    | true, promptText ->
                        return box {| data = [| userTextMessage childId promptText; assistantCompletionMessage childId "done" |] |}
                    | false, _ ->
                        return box {| data = [| assistantCompletionMessage "chain-session" "done" |] |}
                }))
            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ()))
        ]) ]
    let! p = plugin (box {| directory = workspaceDir; client = mockClient; nowMs = dayMs "2026-06-15" |})
    let knowledgeGraphRuntime = get (pluginKnowledgeGraphRuntime p) "rawInstance" :?> KnowledgeGraphRuntime

    do! knowledgeGraphRuntime.StartMaintenanceIfDue(workspaceDir, parentSessionID = "chain-parent")
    do! waitForBackgroundJobsForTesting p
    let launches1 = takeBookkeeperLaunchesForTesting p
    check "first maintenance schedules day 8" (launches1.Length = 1 && (str launches1.[0] "prompt").Contains "2026-06-08")
    check "first maintenance launch keeps root parent" (str (get createCalls.[0] "body") "parentID" = "chain-parent")

    let! submit1 = knowledgeGraphRuntime.Submit(childIds.[0], [])
    check "submit day 8 succeeds" (submit1.Contains "Rewrote knowledge graph day 2026-06-08")
    do! waitForBackgroundJobsForTesting p
    let launches2 = takeBookkeeperLaunchesForTesting p
    check "submit day 8 triggers day 9" (launches2.Length = 1 && (str launches2.[0] "prompt").Contains "2026-06-09")
    check "submit day 8 keeps chained parent at root" (str (get createCalls.[1] "body") "parentID" = "chain-parent")

    let! submit2 = knowledgeGraphRuntime.Submit(childIds.[1], [])
    check "submit day 9 succeeds" (submit2.Contains "Rewrote knowledge graph day 2026-06-09")
    do! waitForBackgroundJobsForTesting p
    let launches3 = takeBookkeeperLaunchesForTesting p
    check "submit day 9 does not schedule more maintenance" (launches3.Length = 0)

    do! rmAsync workspaceDir
}
