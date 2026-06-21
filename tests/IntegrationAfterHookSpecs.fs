module VibeFs.Tests.IntegrationAfterHookSpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Message
open VibeFs.Mux.Plugin
open VibeFs.Opencode.Plugin
open VibeFs.Mux.AiSettings
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.WikiFiles

let afterHookRecordsDirectWriteSpec () = promise {
    let! workspaceDir = mkdtempAsync "after-hook-write-"
    do! ensureWikiDir workspaceDir
    let mockClient = bookkeeperMockClient [| assistantCompletionMessage "turn-1" "Patched files" |]
    let! pluginObject = plugin (box {| directory = workspaceDir; client = mockClient |})
    let toolExecuteAfter = get pluginObject "tool.execute.after"

    let writeInput =
        createObj [ "tool", box "write"
                    "sessionID", box "turn-1"
                    "callID", box "write-call-1"
                    "args", box (createObj [ "file_path", box "src/turn.fs"; "content", box "let turn = 1" ]) ]
    let writeOutput = createObj [ "output", box "Successfully wrote to src/turn.fs" ]
    do! toolExecuteAfter $ (writeInput, writeOutput) |> unbox<JS.Promise<unit>>

    let patchInput =
        createObj [ "tool", box "apply_patch"
                    "sessionID", box "turn-1"
                    "callID", box "patch-call-1"
                    "args", box (createObj [ "patchText", box "*** Begin Patch\n*** Update File: src/turn.fs\n@@\n-let turn = 0\n+let turn = 1\n*** End Patch" ]) ]
    let patchOutput = createObj [ "output", box "Applied patch to src/turn.fs" ]
    do! toolExecuteAfter $ (patchInput, patchOutput) |> unbox<JS.Promise<unit>>

    do! waitForBackgroundJobsForTesting pluginObject
    let launches = takeBookkeeperLaunchesForTesting pluginObject
    check "after-hook records each write call once" (launches.Length = 2)
    check "after-hook write uses bookkeeper agent" (launches |> Array.forall (fun l -> str l "agent" = "bookkeeper"))
    check "after-hook write launch carries file path and content as input" (
        launches |> Array.exists (fun l -> (str l "prompt").Contains "src/turn.fs" && (str l "prompt").Contains "let turn = 1"))
    check "after-hook patch launch carries patch text as input" (
        launches |> Array.exists (fun l -> (str l "prompt").Contains "*** Update File: src/turn.fs"))
    check "after-hook write launch carries tool output as result" (
        launches |> Array.exists (fun l -> (str l "result").Contains "Successfully wrote to src/turn.fs"))
    do! rmAsync workspaceDir
}

let afterHookSkipsChildSessionSpec () = promise {
    let mockClient =
        createObj [ "session", box (createObj [
            "create", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (promise { return box {| data = box {| id = "child-coder-session" |} |} })))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun _ -> (Promise.lift ())))
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (promise { return box {| data = [|
                    box {| info = box {| role = "assistant" |}; parts = [| box {| ``type`` = "text"; text = "Coder finished" |} |] |}
                |] |} })))
            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> (Promise.lift ())))
        ]) ]
    let! workspaceDir = mkdtempAsync "after-hook-child-skip-"
    do! ensureWikiDir workspaceDir
    let! pluginObject = plugin (box {| directory = workspaceDir; client = mockClient |})
    let coder = get (get pluginObject "tool") "coder"
    let intents : obj array = [|
        createObj [ "objective", box "fix bug"; "background", box "bg"; "targets", box [| createObj [ "file", box "a.ts"; "guide", box "g" ] |] ]
    |]
    let! _ = (get coder "execute") $ (createObj [ "intents", box intents ], createObj [ "directory", box workspaceDir; "sessionID", box "coder-parent"; "abort", box null ]) |> unbox<JS.Promise<string>>

    let toolExecuteAfter = get pluginObject "tool.execute.after"
    let childWriteInput =
        createObj [ "tool", box "write"
                    "sessionID", box "child-coder-session"
                    "callID", box "child-write-1"
                    "args", box (createObj [ "file_path", box "src/internal.fs"; "content", box "internal" ]) ]
    let childWriteOutput = createObj [ "output", box "Successfully wrote to src/internal.fs" ]
    do! toolExecuteAfter $ (childWriteInput, childWriteOutput) |> unbox<JS.Promise<unit>>

    do! waitForBackgroundJobsForTesting pluginObject
    let launches = takeBookkeeperLaunchesForTesting pluginObject
    check "after-hook skips bookkeeping for tools inside child-agent sessions" (launches.Length = 0)
    do! rmAsync workspaceDir
}

let afterHookSkipsFailedToolSpec () = promise {
    let! workspaceDir = mkdtempAsync "after-hook-failed-"
    do! ensureWikiDir workspaceDir
    let mockClient = bookkeeperMockClient [| assistantCompletionMessage "fail-turn" "noted" |]
    let! pluginObject = plugin (box {| directory = workspaceDir; client = mockClient |})
    let toolExecuteAfter = get pluginObject "tool.execute.after"
    let writeInput =
        createObj [ "tool", box "write"
                    "sessionID", box "fail-turn"
                    "callID", box "write-fail-1"
                    "args", box (createObj [ "file_path", box "src/fail.fs"; "content", box "boom" ]) ]
    let failedOutput = createObj [ "output", box ""; "error", box "permission denied" ]
    do! toolExecuteAfter $ (writeInput, failedOutput) |> unbox<JS.Promise<unit>>
    do! waitForBackgroundJobsForTesting pluginObject
    let launches = takeBookkeeperLaunchesForTesting pluginObject
    check "after-hook skips bookkeeping when tool reports an error" (launches.Length = 0)
    do! rmAsync workspaceDir
}

let afterHookRecordsCoderSpec () = promise {
    let createCalls = ResizeArray<obj>()
    let mockClient =
        createObj [ "session", box (createObj [
            "create", box (System.Func<obj, JS.Promise<obj>>(fun arg ->
                (promise { createCalls.Add(arg); return box {| data = box {| id = "child-bk-session" |} |} })))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun _ -> (Promise.lift ())))
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (promise { return box {| data = [|
                    box {| info = box {| role = "assistant" |}; parts = [| box {| ``type`` = "text"; text = "bk" |} |] |}
                |] |} })))
            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> (Promise.lift ())))
        ]) ]
    let! workspaceDir = mkdtempAsync "after-hook-coder-"
    do! ensureWikiDir workspaceDir
    let! p = plugin (box {| directory = workspaceDir; client = mockClient |})
    let toolExecuteAfter = get p "tool.execute.after"
    let coderInput =
        createObj [ "tool", box "coder"
                    "sessionID", box "coder-parent"
                    "callID", box "coder-call-1"
                    "args", box (createObj [ "intents", box [| createObj [ "objective", box "fix bug" ] |] ]) ]
    let coderOutput = createObj [ "output", box "Coder finished" ]
    do! toolExecuteAfter $ (coderInput, coderOutput) |> unbox<JS.Promise<unit>>

    let launches = takeBookkeeperLaunchesForTesting p
    check "after-hook records coder tool once" (launches.Length = 1)
    check "after-hook coder launch agent" (str launches.[0] "agent" = "bookkeeper")
    check "after-hook coder launch records input" (str launches.[0] "prompt" <> "")
    check "after-hook coder launch records output" ((str launches.[0] "result").Contains "Coder finished")
    do! waitForBackgroundJobsForTesting p
    check "after-hook coder bookkeeper child flattens to user-facing parent" (
        createCalls |> Seq.exists (fun call -> str (get call "body") "parentID" = "coder-parent"))
    do! rmAsync workspaceDir
}

let afterHookRecordsExecutorSpec () = promise {
    let! workspaceDir = mkdtempAsync "executor-bookkeeper-"
    do! ensureWikiDir workspaceDir
    let! p = plugin (box {| directory = workspaceDir |})
    check "executor tool exposes mode" (not (isNullish (executorModeSchema p)))
    let toolExecuteAfter = get p "tool.execute.after"
    let fire mode =
        let input =
            createObj [ "tool", box "executor"
                        "sessionID", box "executor-session"
                        "callID", box ("exec-" + mode)
                        "args", box (createObj [ "language", box "shell"; "program", box ("printf " + mode); "timeout_type", box "short"; "mode", box mode ]) ]
        let output = createObj [ "output", box "ok" ]
        toolExecuteAfter $ (input, output) |> unbox<JS.Promise<unit>>
    do! fire "ro"
    do! fire "rw"
    do! waitForBackgroundJobsForTesting p
    let launches = takeBookkeeperLaunchesForTesting p
    check "after-hook skips read-only executor calls, records only rw" (launches.Length = 1)
    check "after-hook executor launch agent" (launches |> Array.forall (fun l -> str l "agent" = "bookkeeper"))
    check "after-hook records only the rw executor call" (
        launches |> Array.forall (fun l -> (str l "prompt").Contains "printf rw"))
    do! rmAsync workspaceDir
}
