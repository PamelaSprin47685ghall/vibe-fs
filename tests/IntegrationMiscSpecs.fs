module VibeFs.Tests.IntegrationMiscSpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Wiki
open VibeFs.Mux.Plugin
open VibeFs.Opencode.Plugin
open VibeFs.Opencode.AgentConfig
open VibeFs.Opencode.WikiRuntime
open VibeFs.Mux.AiSettings
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.WikiFiles


let writeToolSpec (reg: obj) = promise {
    let tools = unbox<obj[]> (get reg "tools")
    let writeDef = tools |> Array.find (fun t -> str t "name" = "write")
    let! missingPath = (get writeDef "execute") $ (createObj [ "cwd", box "/tmp" ], createObj [ "content", box "x" ]) |> unbox<JS.Promise<string>>
    check "write missing file_path error" (missingPath.Contains "file_path")
    let! tmpDir = mkdtempAsync "write-test-"
    let! writeResult = (get writeDef "execute") $ (createObj [ "cwd", box tmpDir ], createObj [ "file_path", box "empty.txt"; "content", box "" ]) |> unbox<JS.Promise<string>>
    check "write empty string succeeds" (writeResult.Contains "Successfully wrote")
    do! rmAsync tmpDir
}

let loopCommandSpec (reg: obj) = promise {
    let cmds = unbox<obj[]> (get reg "slashCommands")
    let loopCmd = cmds |> Array.find (fun c -> str c "key" = "loop")
    let! result = (get loopCmd "execute") $ ("test-ws", "some task") |> unbox<JS.Promise<string>>
    check "loop resolve includes task" (result.Contains "some task")
}

let agentConfigSpec () = promise {
    let! workspaceDir = mkdtempAsync "agent-config-"
    let! p = plugin (box {| directory = workspaceDir |})
    let cfgInput =
        box {|
            agent = box {|
                browser = box {| model = "kimi-for-coding/k2p7" |}
                executor = box {| model = "opencode-go/deepseek-v4-flash" |}
                custom = box {| model = "custom-model" |}
            |}
        |}
    let! cfg = (get p "config") $ cfgInput |> unbox<JS.Promise<obj>>
    let agents = get cfg "agent"
    let browser = get agents "browser"
    check "browser prompt empty" (str browser "prompt" = "")
    check "browser mode subagent" (str browser "mode" = "subagent")
    let executor = get agents "executor"
    check "executor mode subagent" (str executor "mode" = "subagent")
    let custom = get agents "custom"
    check "custom model preserved" (str custom "model" = "custom-model")
    let manager = get agents "manager"
    check "manager mode primary" (str manager "mode" = "primary")
    do! rmAsync workspaceDir
}

let bookkeeperAgentConfigSpec () = promise {
    let! workspaceDir = mkdtempAsync "bookkeeper-agent-config-"
    let! p = plugin (box {| directory = workspaceDir |})
    let! cfg = (get p "config") $ (createObj []) |> unbox<JS.Promise<obj>>
    let agents = get cfg "agent"
    check "bookkeeper agent exists" (not (isNullish (get agents "bookkeeper")))
    check "bookkeeper agent mode subagent" (str (get agents "bookkeeper") "mode" = "subagent")
    do! rmAsync workspaceDir
}

let disableMimoMemoryAndCheckpointSpec () = promise {
    let cfg = createObj []
    let next = disableMimoMemoryAndCheckpoint cfg
    let agents = get next "agent"
    check "dream agent disabled" (truthy (get (get agents "dream") "disable"))
    check "distill agent disabled" (truthy (get (get agents "distill") "disable"))
    check "checkpoint-writer agent disabled" (truthy (get (get agents "checkpoint-writer") "disable"))
    let checkpoint = get next "checkpoint"
    let thresholds = get checkpoint "thresholds"
    check "checkpoint.thresholds empty array"
        (isArray thresholds && (unbox<obj[]> thresholds).Length = 0)
    let pushCaps = get checkpoint "push_caps"
    for cap in [|
        "tasks_ledger"; "focus_task"; "actor_ledger"; "memory_titles"
        "global"; "checkpoint"; "memory"; "notes"
        "design_decisions"; "open_notes" |] do
        check $"checkpoint.push_caps.{cap}=0" (unbox<int> (get pushCaps cap) = 0)
    check "checkpoint.memory_reconcile_on_search=false"
        (unbox<bool> (get checkpoint "memory_reconcile_on_search") = false)
    check "dream.auto=false" (unbox<bool> (get (get next "dream") "auto") = false)
    check "distill.auto=false" (unbox<bool> (get (get next "distill") "auto") = false)
    check "memory.cc_index=false" (unbox<bool> (get (get next "memory") "cc_index") = false)
}

let disableMimoMemoryAndCheckpointPreservesUserAgentSpec () = promise {
    let cfg =
        createObj [
            "agent", box (createObj [
                "dream", box {| model = "user-model"; prompt = "user-prompt" |}
            ])
            "checkpoint", box {| thresholds = [| "10%" |] |}
        ]
    let next = disableMimoMemoryAndCheckpoint cfg
    let dream = get (get next "agent") "dream"
    check "user dream model preserved" (str dream "model" = "user-model")
    check "user dream prompt preserved" (str dream "prompt" = "user-prompt")
    check "user dream disable injected" (truthy (get dream "disable"))
    let checkpoint = get next "checkpoint"
    let thresholds = get checkpoint "thresholds"
    check "user checkpoint.thresholds overridden empty"
        (isArray thresholds && (unbox<obj[]> thresholds).Length = 0)
}

let pluginConfigHookDisablesMimoMemoryAndCheckpointSpec () = promise {
    let! workspaceDir = mkdtempAsync "plugin-disable-mimo-"
    let! p = plugin (box {| directory = workspaceDir |})
    let! cfg = (get p "config") $ (createObj []) |> unbox<JS.Promise<obj>>
    let agents = get cfg "agent"
    check "config hook dream disabled" (truthy (get (get agents "dream") "disable"))
    check "config hook distill disabled" (truthy (get (get agents "distill") "disable"))
    check "config hook checkpoint-writer disabled" (truthy (get (get agents "checkpoint-writer") "disable"))
    let checkpoint = get cfg "checkpoint"
    let thresholds = get checkpoint "thresholds"
    check "config hook checkpoint.thresholds empty"
        (isArray thresholds && (unbox<obj[]> thresholds).Length = 0)
    check "config hook dream.auto=false" (unbox<bool> (get (get cfg "dream") "auto") = false)
    check "config hook distill.auto=false" (unbox<bool> (get (get cfg "distill") "auto") = false)
    check "config hook memory.cc_index=false" (unbox<bool> (get (get cfg "memory") "cc_index") = false)
    do! rmAsync workspaceDir
}

let executorModeSchemaSpec () = promise {
    let! workspaceDir = mkdtempAsync "executor-mode-schema-"
    let! p = plugin (box {| directory = workspaceDir |})
    let modeSchema = executorModeSchema p
    check "executor mode schema exists" (not (isNullish modeSchema))
    check "executor mode schema exposes mode" (not (isNullish modeSchema))
    check "executor mode schema enum ro/rw" (enumValues modeSchema = [| "ro"; "rw" |])
    do! rmAsync workspaceDir
}

let executorActorSpec () = promise {
    let seen = System.Collections.Generic.List<string>()
    let releaseRequested = ref false
    let gateResolve = ref (fun () -> ())
    let gateAsync : JS.Promise<unit> =
        Promise.create (fun resolve _ ->
            gateResolve.Value <- resolve
            if releaseRequested.Value then resolve ())
    let first = post "session-1" (fun () ->
        promise {
            seen.Add "first-start"
            do! gateAsync
            seen.Add "first-end"
            return "one"
        })
    let second = post "session-1" (fun () ->
        promise {
            seen.Add "second-start"
            seen.Add "second-end"
            return "two"
        })
    releaseRequested.Value <- true
    gateResolve.Value ()
    let! _ = first
    let! _ = second
    check "executor actor preserves order" (seen |> Seq.toArray = [| "first-start"; "first-end"; "second-start"; "second-end" |])
}

let wikiWorkspaceSerializationSpec () = promise {
    let seen = System.Collections.Generic.List<string>()
    /// WikiRuntime serializes per-workspace writes through one SerialQueue per
    /// workspace (P51 removed the WikiActor class wrapper); this pins the
    /// ordering guarantee of that queue directly.
    let queue = VibeFs.Shell.PromiseQueue.SerialQueue()
    let releaseRequested = ref false
    let gateResolve = ref (fun () -> ())
    let gateAsync : JS.Promise<unit> =
        Promise.create (fun resolve _ ->
            gateResolve.Value <- resolve
            if releaseRequested.Value then resolve ())
    queue.Enqueue(fun () -> promise {
        seen.Add "first-start"
        do! gateAsync
        seen.Add "first-end"
    }) |> Promise.start
    queue.Enqueue(fun () -> promise {
        seen.Add "second-start"
        seen.Add "second-end"
    }) |> Promise.start
    releaseRequested.Value <- true
    gateResolve.Value ()
    let! _ = queue.Enqueue(fun () -> promise { return "" })
    check "wiki workspace serialization preserves order" (seen |> Seq.toArray = [| "first-start"; "first-end"; "second-start"; "second-end" |])
}

let wikiPortLockTimeoutSpec () = promise {
    let! workspaceDir = mkdtempAsync "wiki-port-lock-timeout-"
    do! ensureWikiDir workspaceDir
    let port = VibeFs.Shell.WikiPortLock.lockPortForPath workspaceDir
    let net = requireFn("node:net")
    let server = net?createServer()
    do! Promise.create(fun resolve reject ->
        server?once("listening", System.Func<unit>(fun () -> resolve ())) |> ignore
        server?once("error", System.Func<obj, unit>(fun error -> reject (exn (string error)))) |> ignore
        server?listen(port, "127.0.0.1") |> ignore)
    let wikiRuntime = WikiRuntime(null, workspaceDir, (fun () -> System.DateTime.UtcNow), ChildAgentRegistry.Create(), 0L, 0)
    let marker = renderJobMarker { workspaceRoot = workspaceDir; kind = AppendAfterWork }
    let sessionID = "wiki-lock-session"
    let lockClient =
        createObj [ "session", box (createObj [
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (promise { return box {| data = [| userTextMessage sessionID marker |] |} })))
        ]) ]
    let lockRuntime = WikiRuntime(lockClient, workspaceDir, (fun () -> System.DateTime.UtcNow), ChildAgentRegistry.Create(), 0L, 0)
    let submitAttempt = lockRuntime.SubmitFromHistory(sessionID, workspaceDir, [])
    let! errorText =
        submitAttempt
        |> Promise.map (fun _ -> "unexpected-success")
        |> Promise.catch (fun err -> string err)
    check "wiki port lock timeout surfaces explicit error" (errorText.Contains "Timed out acquiring wiki port lock")
    do! Promise.create(fun resolve _ -> server?close(System.Func<unit>(fun () -> resolve ())) |> ignore)
    do! rmAsync workspaceDir
}
