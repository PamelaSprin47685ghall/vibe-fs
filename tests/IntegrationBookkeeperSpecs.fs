module VibeFs.Tests.IntegrationBookkeeperSpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Tests.IntegrationMuxSetup

open VibeFs.Mux.Plugin
open VibeFs.Opencode.Plugin
open VibeFs.Opencode.KnowledgeGraphRuntime
open VibeFs.Mux.AiSettings
open VibeFs.Mux.KnowledgeGraphTools
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Shell.Dyn

let bookkeeperLaunchCarriesAiSettingsSpec () = promise {
    let createCalls = ResizeArray<obj>()
    let promptCalls = ResizeArray<obj>()
    let mockClient =
        createObj [ "session", box (createObj [
            "create", box (System.Func<obj, JS.Promise<obj>>(fun arg ->
                (promise { createCalls.Add(arg); return box {| data = box {| id = "child-bk-ai-session" |} |} })))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                (promise { promptCalls.Add(arg) })))
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (promise { return box {| data = [| userTextMessage "child-bk-ai-session" (renderJobMarker { workspaceRoot = "/tmp"; kind = AppendAfterWork }); box {| info = box {| role = "assistant" |}; parts = [| box {| ``type`` = "tool"; tool = "return_bookkeeper" |} |] |} |] |} })))
            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ()))
        ]) ]
    let! workspaceDir = mkdtempAsync "bookkeeper-ai-settings-"
    do! ensureKnowledgeGraphDir workspaceDir
    let! p = plugin (box {| directory = workspaceDir; client = mockClient |})
    let knowledgeGraphRuntime = get (pluginKnowledgeGraphRuntime p) "rawInstance" :?> KnowledgeGraphRuntime
    let aiSettings : DelegatedAiSettings = { modelString = Some "openai/gpt-5"; thinkingLevel = Some "high" }
    knowledgeGraphRuntime.StartBookkeeperAppend("input", "result", "Title", parentSessionID = "parent-session", aiSettings = aiSettings)
    do! waitForBackgroundJobsForTesting p
    check "bookkeeper aiSettings create keeps parentID" (str (get createCalls.[0] "body") "parentID" = "parent-session")
    check "bookkeeper session title is stable" (str (get createCalls.[0] "body") "title" = "Bookkeeper")
    let promptBody = get promptCalls.[0] "body"
    let modelObj = get promptBody "model"
    check "bookkeeper aiSettings prompt carries model" (str modelObj "providerID" = "openai" && str modelObj "modelID" = "gpt-5")
    check "bookkeeper aiSettings prompt carries thinking variant" (str promptBody "variant" = "high")
    do! rmAsync workspaceDir
}

let bookkeeperFireAndForgetSpec () = promise {
    let promptCompleted = ResizeArray<bool>()
    let mockClient =
        createObj [
            "session", box (createObj [
                "create", box (System.Func<obj, JS.Promise<obj>>(fun _ -> (promise { return box {| data = box {| id = "child-ff-session" |} |} })))
                "prompt", box (System.Func<obj, JS.Promise<unit>>(fun _ -> (promise { promptCompleted.Add(true) })))
                "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ -> (promise {
                    let msg = box {| info = box {| role = "assistant" |}; parts = [| box {| ``type`` = "text"; text = "done" |} |] |}
                    return box {| data = [| msg |] |}
                })))
                "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> (Promise.lift ())))
            ])
        ]
    let! workspaceDir = mkdtempAsync "bookkeeper-fireforget-"
    do! ensureKnowledgeGraphDir workspaceDir
    let! p = plugin (box {| directory = workspaceDir; client = mockClient |})
    let toolExecuteAfter = get p "tool.execute.after"
    let coderInput =
        createObj [ "tool", box "coder"
                    "sessionID", box "ff-parent"
                    "callID", box "ff-call-1"
                    "args", box (createObj [ "intents", box [| createObj [ "objective", box "do work" ] |] ]) ]
    let coderOutput = createObj [ "output", box "Coder finished" ]
    do! toolExecuteAfter $ (coderInput, coderOutput) |> unbox<JS.Promise<unit>>
    let launches = takeBookkeeperLaunchesForTesting p
    check "fire-and-forget: bookkeeper launch recorded synchronously" (launches.Length = 1)
    do! waitForBackgroundJobsForTesting p
    check "fire-and-forget: bookkeeper prompt ran in background" (promptCompleted.Count >= 1)
    do! rmAsync workspaceDir
}

let websearchTriggersBookkeeperSpec () = promise {
    let! workspaceDir = mkdtempAsync "websearch-bookkeeper-"
    do! ensureKnowledgeGraphDir workspaceDir
    let mockClient = bookkeeperMockClient [| assistantCompletionMessage "child-bookkeeper-session" "noted" |]
    let! p = plugin (box {| directory = workspaceDir; client = mockClient |})
    let toolExecuteAfter = get p "tool.execute.after"
    let input = createObj [ "tool", box "websearch"; "sessionID", box "websearch-parent"; "callID", box "ws-1"
                            "args", box (createObj [ "query", box "ollama"; "what_to_summarize", box "summary" ]) ]
    let output = createObj [ "output", box "search results body" ]
    do! toolExecuteAfter $ (input, output) |> unbox<JS.Promise<unit>>
    let launches = takeBookkeeperLaunchesForTesting p
    check "websearch after-hook records one bookkeeper launch" (launches.Length = 1)
    check "websearch after-hook launch agent" (str launches.[0] "agent" = "bookkeeper")
    check "websearch after-hook prompt carries query and output" (
        (str launches.[0] "prompt").Contains "ollama" && (str launches.[0] "result").Contains "search results body")
    do! waitForBackgroundJobsForTesting p
    do! rmAsync workspaceDir
}

let webfetchTriggersBookkeeperSpec () = promise {
    let! workspaceDir = mkdtempAsync "webfetch-bookkeeper-"
    do! ensureKnowledgeGraphDir workspaceDir
    let mockClient = bookkeeperMockClient [| assistantCompletionMessage "child-bookkeeper-session" "noted" |]
    let! p = plugin (box {| directory = workspaceDir; client = mockClient |})
    let toolExecuteAfter = get p "tool.execute.after"
    let input = createObj [ "tool", box "webfetch"; "sessionID", box "webfetch-parent"; "callID", box "wf-1"
                            "args", box (createObj [ "url", box "https://example.com" ]) ]
    let output = createObj [ "output", box "fetched page content" ]
    do! toolExecuteAfter $ (input, output) |> unbox<JS.Promise<unit>>
    let launches = takeBookkeeperLaunchesForTesting p
    check "webfetch after-hook records one bookkeeper launch" (launches.Length = 1)
    check "webfetch after-hook launch agent" (str launches.[0] "agent" = "bookkeeper")
    check "webfetch after-hook prompt carries url and output" (
        (str launches.[0] "prompt").Contains "https://example.com" && (str launches.[0] "result").Contains "fetched page content")
    do! waitForBackgroundJobsForTesting p
    do! rmAsync workspaceDir
}

let bookkeeperSessionRegisteredInChildAgentRegistrySpec () = promise {
    let mockClient =
        createObj [ "session", box (createObj [
            "create", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (promise { return box {| data = box {| id = "child-bk-session" |} |} })))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun _ -> (Promise.lift ())))
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (promise { return box {| data = [|
                    box {| info = box {| role = "assistant" |}; parts = [| box {| ``type`` = "text"; text = "Coder finished" |} |] |}
                |] |} })))
            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> (Promise.lift ())))
        ]) ]
    let! workspaceDir = mkdtempAsync "bookkeeper-registry-"
    do! ensureKnowledgeGraphDir workspaceDir
    let! p = plugin (box {| directory = workspaceDir; client = mockClient |})
    let toolExecuteAfter = get p "tool.execute.after"
    let coderInput =
        createObj [ "tool", box "coder"
                    "sessionID", box "bk-parent"
                    "callID", box "coder-call-1"
                    "args", box (createObj [ "intents", box "fix bug" ]) ]
    let coderOutput = createObj [ "output", box "Coder finished" ]
    do! toolExecuteAfter $ (coderInput, coderOutput) |> unbox<JS.Promise<unit>>
    do! waitForBackgroundJobsForTesting p

    let chatMessage = get p "chat.message"
    let tools = createObj [ "return_bookkeeper", box true; "websearch", box true ]
    let message = createObj [ "tools", box tools ]
    let output = createObj [ "message", box message; "parts", box [||] ]
    let input = createObj [ "sessionID", box "child-bk-session" ]
    do! chatMessage $ (input, output) |> unbox<JS.Promise<unit>>

    let resolvedTools = get (get output "message") "tools"
    check "bookkeeper session keeps return_bookkeeper enabled" (unbox<bool> (get resolvedTools "return_bookkeeper") = true)
    check "bookkeeper session denies unrelated tools via permission matrix" (unbox<bool> (get resolvedTools "websearch") = false)
    do! rmAsync workspaceDir
}

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
                p.StartsWith("---\n") && p.Contains("type: \"vibe_knowledge_graph_job\"")))
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
                && prompt.Contains("type: \"vibe_knowledge_graph_job\"")
                && prompt.Contains("workspaceRoot: \"" + workspaceDir + "\"")
                && prompt.Contains("kind: \"daily\"")
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

let hintLine = "// HINT: Do you need to update todo?"

let bookkeeperAfterHookAddsHintToOutputSpec () = promise {
    let! workspaceDir = mkdtempAsync "bookkeeper-hint-"
    do! ensureKnowledgeGraphDir workspaceDir
    let mockClient = bookkeeperMockClient [| assistantCompletionMessage "child-hint-session" "noted" |]
    let! p = plugin (box {| directory = workspaceDir; client = mockClient |})
    let toolExecuteAfter = get p "tool.execute.after"
    let input =
        createObj [ "tool", box "websearch"
                    "sessionID", box "hint-parent"
                    "callID", box "hint-1"
                    "args", box (createObj [ "query", box "ollama"; "what_to_summarize", box "summary" ]) ]
    let output = createObj [ "output", box "search results body" ]
    do! toolExecuteAfter $ (input, output) |> unbox<JS.Promise<unit>>
    let launches = takeBookkeeperLaunchesForTesting p
    let afterOutput = str output "output"
    check "hint: bookkeeper launch records one" (launches.Length = 1)
    check "hint: bookkeeper launch result stays original" ((str launches.[0] "result") = "search results body")
    check "hint: bookkeeper launch result excludes HINT" (not ((str launches.[0] "result").Contains hintLine))
    check "hint: main agent output starts with HINT" (afterOutput.StartsWith hintLine)
    check "hint: main agent output preserves original body" (afterOutput.Contains "search results body")
    do! rmAsync workspaceDir
}

let bookkeeperAfterHookSkipsHintOnNonBookkeepingToolSpec () = promise {
    let! workspaceDir = mkdtempAsync "bookkeeper-hint-skip-"
    do! ensureKnowledgeGraphDir workspaceDir
    let mockClient = bookkeeperMockClient [| assistantCompletionMessage "child-skip-session" "noted" |]
    let! p = plugin (box {| directory = workspaceDir; client = mockClient |})
    let toolExecuteAfter = get p "tool.execute.after"
    let input =
        createObj [ "tool", box "fuzzy_find"
                    "sessionID", box "skip-parent"
                    "callID", box "skip-1"
                    "args", box (createObj [ "query", box "x" ]) ]
    let output = createObj [ "output", box "fuzzy result body" ]
    do! toolExecuteAfter $ (input, output) |> unbox<JS.Promise<unit>>
    let launches = takeBookkeeperLaunchesForTesting p
    check "hint: non-bookkeeping tool skips launch" (launches.Length = 0)
    check "hint: non-bookkeeping tool keeps original output" ((str output "output") = "fuzzy result body")
    do! rmAsync workspaceDir
}

let bookkeeperAfterHookSkipsHintOnFailureSpec () = promise {
    let! workspaceDir = mkdtempAsync "bookkeeper-hint-fail-"
    do! ensureKnowledgeGraphDir workspaceDir
    let mockClient = bookkeeperMockClient [| assistantCompletionMessage "child-fail-session" "noted" |]
    let! p = plugin (box {| directory = workspaceDir; client = mockClient |})
    let toolExecuteAfter = get p "tool.execute.after"
    let input =
        createObj [ "tool", box "websearch"
                    "sessionID", box "fail-parent"
                    "callID", box "fail-1"
                    "args", box (createObj [ "query", box "x" ]) ]
    let output = createObj [ "output", box "search results body"; "error", box "boom" ]
    do! toolExecuteAfter $ (input, output) |> unbox<JS.Promise<unit>>
    let launches = takeBookkeeperLaunchesForTesting p
    check "hint: failed tool skips launch" (launches.Length = 0)
    check "hint: failed tool keeps original output" ((str output "output") = "search results body")
    do! rmAsync workspaceDir
}

let muxBookkeeperAfterHookAddsHintToOutputSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-bookkeeper-hint-"
    do! ensureKnowledgeGraphDir workspaceDir
    let prompts = ResizeArray<string>()
    let deps = minimalMuxDeps ()
    deps?("directory") <- workspaceDir
    deps?("taskService") <- mockMuxTaskServiceCapturingPrompt prompts
    let reg = createRegistration deps
    let after = get reg "tool.execute.after"
    if isNullish after then
        check "mux hint: exposes tool.execute.after" false
    else
        let input =
            createObj
                [ "tool", box "websearch"
                  "sessionID", box "mux-hint-parent"
                  "callID", box "mux-hint-1"
                  "args", box (createObj [ "query", box "ollama" ]) ]
        let output = createObj [ "output", box "search results body" ]
        do! after $ (input, output) |> unbox<JS.Promise<unit>>
        let launches = takeBookkeeperLaunchesForTesting reg
        let afterOutput = str output "output"
        check "mux hint: bookkeeper launch records one" (launches.Length = 1)
        check "mux hint: bookkeeper launch result stays original" ((str launches.[0] "result") = "search results body")
        check "mux hint: bookkeeper launch result excludes HINT" (not ((str launches.[0] "result").Contains hintLine))
        check "mux hint: main agent output starts with HINT" (afterOutput.StartsWith hintLine)
        do! waitForBackgroundJobsForTesting reg
        check "mux hint: delegated taskService prompt excludes HINT" (
            prompts
            |> Seq.forall (fun prompt -> not (prompt.Contains hintLine)))
    do! rmAsync workspaceDir
}
