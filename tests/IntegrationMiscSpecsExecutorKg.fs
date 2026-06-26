module Wanxiangshu.Tests.IntegrationMiscSpecsExecutorKg

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Kernel.KnowledgeGraph
open Wanxiangshu.Kernel.KnowledgeGraph.Types
open Wanxiangshu.Opencode.KnowledgeGraphRuntime
open Wanxiangshu.Opencode.Plugin
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.KnowledgeGraphFiles
open Wanxiangshu.Shell.Dyn

let executorModeSchemaSpec () = promise {
    let! workspaceDir = mkdtempAsync "executor-mode-schema-"
    let! p = plugin (box {| directory = workspaceDir |})
    let modeSchema = executorModeSchema p
    check "executor mode schema exists" (not (isNullish modeSchema))
    check "executor mode schema exposes mode" (not (isNullish modeSchema))
    check "executor mode schema enum ro/rw" (enumValues modeSchema = [| "ro"; "rw" |])
    let languageSchema = executorLanguageSchema p
    check "executor language schema exists" (not (isNullish languageSchema))
    check "executor language schema enum shell/python/javascript" (enumValues languageSchema = [| "shell"; "python"; "javascript" |])
    do! rmAsync workspaceDir
}

let executorRejectsInvalidLanguageSpec () = promise {
    let! workspaceDir = mkdtempAsync "executor-invalid-language-"
    let! p = plugin (box {| directory = workspaceDir |})
    let executor = executorDefinition p
    let args =
        createObj [
            "language", box "ruby"
            "program", box "printf should-not-run"
            "timeout_type", box "short"
            "mode", box "ro"
        ]
    let context = createObj [ "directory", box workspaceDir; "sessionID", box "mimo-invalid-language"; "abort", box null ]
    let! result = ((get executor "execute") $ (args, context)) |> unbox<JS.Promise<string>>
    check "executor invalid language returns explicit error" (result = "Executor failed: invalid language for tool 'executor': expected shell, python, or javascript")
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
    let exec = Wanxiangshu.Shell.SessionExecutor.createForScope (Wanxiangshu.Shell.RuntimeScope.create ())
    let first = exec.EnqueuePerSession("session-1", fun () ->
        promise {
            seen.Add "first-start"
            do! gateAsync
            seen.Add "first-end"
            return "one"
        })
    let second = exec.EnqueuePerSession("session-1", fun () ->
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

let knowledgeGraphWorkspaceSerializationSpec () = promise {
    let seen = System.Collections.Generic.List<string>()
    let queue = Wanxiangshu.Shell.PromiseQueue.SerialQueue()
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
    check "knowledge graph workspace serialization preserves order" (seen |> Seq.toArray = [| "first-start"; "first-end"; "second-start"; "second-end" |])
}

let knowledgeGraphPortLockTimeoutSpec () = promise {
    let! workspaceDir = mkdtempAsync "kg-port-lock-timeout-"
    do! ensureKnowledgeGraphDir workspaceDir
    let port = Wanxiangshu.Shell.KnowledgeGraphPortLock.lockPortForPath workspaceDir
    let net = requireFn("node:net")
    let server = net?createServer()
    do! Promise.create(fun resolve reject ->
        server?once("listening", System.Func<unit>(fun () -> resolve ())) |> ignore
        server?once("error", System.Func<obj, unit>(fun error -> reject (exn (string error)))) |> ignore
        server?listen(port, "127.0.0.1") |> ignore)
    let marker = renderJobMarker { workspaceRoot = workspaceDir; kind = AppendAfterWork }
    let sessionID = "kg-lock-session"
    let lockClient =
        createObj [ "session", box (createObj [
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (promise { return box {| data = [| userTextMessage sessionID marker |] |} })))
        ]) ]
    let lockRuntime = KnowledgeGraphRuntime(lockClient, workspaceDir, (fun () -> System.DateTime(2026, 6, 25)), ChildAgentRegistry.Create(), 0L, 0)
    let submitAttempt = lockRuntime.SubmitFromHistory(sessionID, workspaceDir, [])
    let! result = submitAttempt
    check "knowledge graph port lock timeout surfaces explicit error" (result.Contains "Timed out acquiring knowledge graph port lock")
    do! Promise.create(fun resolve _ -> server?close(System.Func<unit>(fun () -> resolve ())) |> ignore)
    do! rmAsync workspaceDir
}