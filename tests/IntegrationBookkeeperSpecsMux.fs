module VibeFs.Tests.IntegrationBookkeeperSpecsMux

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Tests.IntegrationMuxSetup
open VibeFs.Mux.Plugin
open VibeFs.Mux.KnowledgeGraphRuntimeMux
open VibeFs.Mux.KnowledgeGraphRuntimeMuxSubmit
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraph.Types
open VibeFs.Shell.Dyn

let muxToolExecuteAfterTriggersBookkeeperSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-tool-after-"
    do! ensureKnowledgeGraphDir workspaceDir
    let prompts = ResizeArray<string>()
    let deps = minimalMuxDeps ()
    deps?("directory") <- workspaceDir
    deps?("workspaceId") <- "mux-tool-after"
    deps?("taskService") <- mockMuxTaskServiceCapturingPrompt prompts
    let reg = createRegistration deps
    let after = get reg "tool.execute.after"
    if isNullish after then
        check "mux plugin exposes tool.execute.after" false
    else
        let coderInput =
            createObj
                [ "tool", box "coder"
                  "sessionID", box "mux-after-parent"
                  "callID", box "mux-coder-1"
                  "args", box (createObj [ "intents", box "do mux work" ]) ]
        let coderOutput = createObj [ "output", box "Coder finished mux" ]
        do! after $ (coderInput, coderOutput) |> unbox<JS.Promise<unit>>
        let launches = takeBookkeeperLaunchesForTesting reg
        check "mux tool.execute.after records a bookkeeper launch" (launches.Length = 1)
        check "mux tool.execute.after launch uses bookkeeper agent" (str launches.[0] "agent" = "bookkeeper")
        do! waitForBackgroundJobsForTesting reg
        check "mux tool.execute.after delegates via taskService" (prompts.Count >= 1)
        check "mux tool.execute.after taskService prompt carries job marker" (
            prompts
            |> Seq.exists (fun p ->
                p.StartsWith("---\n") && p.Contains("type: vibe_knowledge_graph_job")))
    do! rmAsync workspaceDir
}

let muxToolExecuteAfterSkipsReadOnlyExecutorSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-tool-after-ro-"
    do! ensureKnowledgeGraphDir workspaceDir
    let deps = minimalMuxDeps ()
    deps?("directory") <- workspaceDir
    deps?("taskService") <- mockMuxTaskServiceCapturingPrompt (ResizeArray<string>())
    let reg = createRegistration deps
    let after = get reg "tool.execute.after"
    if isNullish after then
        check "mux plugin exposes tool.execute.after (ro)" false
    else
        let roInput =
            createObj
                [ "tool", box "executor"
                  "sessionID", box "mux-ro-parent"
                  "callID", box "mux-exec-ro"
                  "args", box (createObj [ "mode", box "ro" ]) ]
        let roOutput = createObj [ "output", box "ro output" ]
        do! after $ (roInput, roOutput) |> unbox<JS.Promise<unit>>
        let roLaunches = takeBookkeeperLaunchesForTesting reg
        check "mux tool.execute.after skips read-only executor" (roLaunches.Length = 0)
    do! rmAsync workspaceDir
}

let muxDailyMaintenanceLaunchSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-daily-maintenance-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-18") (DayHeader("2026-06-18", false)) [ knowledgeGraphEntry "0a3f" ["积压问题"] "Daily candidate" ]
    let prompts = ResizeArray<string>()
    let deps = minimalMuxDeps ()
    deps?("directory") <- workspaceDir
    deps?("taskService") <- mockMuxTaskServiceCapturingPrompt prompts
    let reg = createRegistration deps
    let knowledgeGraphRuntime = muxKnowledgeGraphRuntime reg
    let startFn = get knowledgeGraphRuntime "startMaintenanceIfDue"
    if isNullish startFn then
        check "mux knowledge graph runtime exposes startMaintenanceIfDue" false
    else
        let runtime = get knowledgeGraphRuntime "rawInstance" :?> MuxKnowledgeGraphRuntime
        let config = createObj [ "directory", box workspaceDir; "workspaceId", box "mux-daily-maintenance"; "taskService", box (get deps "taskService") ]
        runtime.StartBookkeeperAppend("input", "result", "write", config = config)
        do! ((startFn $ workspaceDir) |> unbox<JS.Promise<unit>>)
        do! waitForBackgroundJobsForTesting reg
        let launches = takeBookkeeperLaunchesForTesting reg
        check "mux daily maintenance schedules at least one launch" (launches.Length >= 1)
        check "mux daily maintenance launch uses bookkeeper agent" (launches |> Array.forall (fun l -> str l "agent" = "bookkeeper"))
        check "mux daily maintenance delegate prompt uses frontmatter job marker" (
            prompts
            |> Seq.exists (fun prompt ->
                prompt.StartsWith("---\n")
                && prompt.Contains("type: vibe_knowledge_graph_job")
                && prompt.Contains("workspaceRoot: " + workspaceDir)
                && prompt.Contains("kind: daily")
                && not (prompt.Contains("[vibe-kg-job]"))))
    do! rmAsync workspaceDir
}

let muxDailyRewriteTriggersNextSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-daily-chain-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-08") (DayHeader("2026-06-08", false)) [ knowledgeGraphEntry "b912" ["第八日问题"] "Day 8 candidate" ]
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-09") (DayHeader("2026-06-09", false)) [ knowledgeGraphEntry "c813" ["第九日问题"] "Day 9 candidate" ]

    let prompts = ResizeArray<string>()
    let deps = minimalMuxDeps ()
    deps?("directory") <- workspaceDir
    deps?("taskService") <- mockMuxTaskServiceCapturingPrompt prompts
    let reg = createRegistration deps
    let knowledgeGraphRuntimeObj = muxKnowledgeGraphRuntime reg
    let startFn = get knowledgeGraphRuntimeObj "startMaintenanceIfDue"
    if isNullish startFn then
        check "mux knowledge graph runtime exposes startMaintenanceIfDue" false
    else
        let runtime = get knowledgeGraphRuntimeObj "rawInstance" :?> MuxKnowledgeGraphRuntime
        let config = createObj [ "directory", box workspaceDir; "workspaceId", box "mux-daily-chain"; "taskService", box (get deps "taskService") ]
        do! ((startFn $ workspaceDir) |> unbox<JS.Promise<unit>>)
        let launches1 = takeBookkeeperLaunchesForTesting reg
        check "mux first maintenance schedules day 8" (launches1.Length = 1 && (str launches1.[0] "prompt").Contains "2026-06-08")

        registerMuxKnowledgeGraphJobForTest reg "job-day8" workspaceDir "daily" (createObj [ "date", box "2026-06-08" ])
        let! _ = runtime.Submit("job-day8", workspaceDir, [], config = config)
        do! waitForBackgroundJobsForTesting reg
        let launches2 = takeBookkeeperLaunchesForTesting reg
        check "mux submit day 8 triggers day 9" (launches2.Length = 1 && (str launches2.[0] "prompt").Contains "2026-06-09")
    do! rmAsync workspaceDir
}
