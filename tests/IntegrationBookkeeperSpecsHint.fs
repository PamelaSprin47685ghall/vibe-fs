module VibeFs.Tests.IntegrationBookkeeperSpecsHint

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Tests.IntegrationMuxSetup
open VibeFs.Opencode.Plugin
open VibeFs.Mux.Plugin
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Kernel.ToolOutputInfo
open VibeFs.Shell.Dyn

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
    check "hint: bookkeeper launch result excludes todo hint" (not (hasExactHint (str launches.[0] "result") hintTodoRefresh))
    check "hint: main agent output has YAML front matter" (afterOutput.StartsWith "---")
    check "hint: main agent output includes todo refresh hint" (hasExactHint afterOutput hintTodoRefresh)
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
        check "mux hint: bookkeeper launch result excludes todo hint" (not (hasExactHint (str launches.[0] "result") hintTodoRefresh))
        check "mux hint: main agent output has YAML front matter" (afterOutput.StartsWith "---")
        check "mux hint: main agent output includes todo refresh hint" (hasExactHint afterOutput hintTodoRefresh)
        do! waitForBackgroundJobsForTesting reg
        check "mux hint: delegated taskService prompt excludes todo refresh hint" (
            prompts
            |> Seq.forall (fun prompt -> not (prompt.Contains hintTodoRefresh)))
    do! rmAsync workspaceDir
}

let muxToolExecuteAfterSkipsChildWorkspaceSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-tool-after-child-"
    do! ensureKnowledgeGraphDir workspaceDir
    let prompts = ResizeArray<string>()
    let deps = minimalMuxDeps ()
    deps?("directory") <- workspaceDir
    deps?("workspaceId") <- "child-session"
    deps?("taskService") <- mockMuxTaskServiceCapturingPrompt prompts
    deps?("findWorkspaceEntry") <-
        box (System.Func<obj, string, obj>(fun _ workspaceId ->
            if workspaceId = "child-session" then
                createObj [ "workspace", box (createObj [ "parentWorkspaceId", box "parent-ws" ]) ]
            else createObj [ "workspace", null ]))
    let reg = createRegistration deps
    let after = get reg "tool.execute.after"
    if isNullish after then
        check "mux plugin exposes tool.execute.after (child)" false
    else
        let writeInput =
            createObj
                [ "tool", box "write"
                  "sessionID", box "child-session"
                  "callID", box "mux-child-write-1"
                  "args", box (createObj [ "path", box "child.txt"; "content", box "hi" ]) ]
        let writeOutput = createObj [ "output", box "wrote file" ]
        do! after $ (writeInput, writeOutput) |> unbox<JS.Promise<unit>>
        let launches = takeBookkeeperLaunchesForTesting reg
        check "mux tool.execute.after skips bookkeeper for child workspace" (launches.Length = 0)
    do! rmAsync workspaceDir
}
