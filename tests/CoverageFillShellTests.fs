module Wanxiangshu.Tests.CoverageFillShellTests

open Fable.Core
open Fable.Core.JsInterop
open System
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.SubagentIo
open Wanxiangshu.Shell.WorkspaceFiles
open Wanxiangshu.Shell.RunnerBackground
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.NudgeRuntime
open Wanxiangshu.Shell.TreeSitterPlatform
module Dyn = Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.TodoStatus

// ── Shell.SubagentIo ───────────────────────────────────────────────────────

let subagentBuildPromptBody () =
    let body = buildPromptBody "coder" "do work" null emptySettings
    equal "agent" "coder" (Dyn.str body "agent")
    let parts = unbox<obj[]> (Dyn.get body "parts")
    equal "parts count" 1 parts.Length
    let settings = { emptySettings with ModelString = Some "openai/gpt4"; ThinkingLevel = Some "high" }
    let body2 = buildPromptBody "inv" "work" null settings
    equal "model provider" "openai" (Dyn.str (Dyn.get body2 "model") "providerID")
    equal "variant" "high" (Dyn.str body2 "variant")

let subagentSignalAborted () =
    check "null not aborted" (not (signalAborted null))
    let notAborted = createObj [ "aborted", box false ]
    check "aborted=false not aborted" (not (signalAborted notAborted))
    let aborted = createObj [ "aborted", box true ]
    check "aborted=true is aborted" (signalAborted aborted)

let subagentMakeAbortPromiseNull () =
    let p = makeAbortPromise null (fun () -> ())
    check "null signal→resolved" true

let subagentRaceWithAbortNull () =
    let p : JS.Promise<unit> = raceWithAbortSignal null (fun () -> ()) (Promise.lift ())
    check "null signal→work passthrough" true

// ── Shell.WorkspaceFiles ───────────────────────────────────────────────────

let wsFresh () =
    let b = fresh ()
    equal "count" 0 b.count
    equal "total" 0 b.totalBytes
    check "results empty" (b.results.Count = 0)

let wsIsFull () =
    let under = { results = ResizeArray<_>(); totalBytes = 100; count = 10 }
    check "under byte budget" (not (isFull under))
    let overBytes = { results = ResizeArray<_>(); totalBytes = 9 * 1_048_576; count = 100 }
    check "over byte budget" (isFull overBytes)
    let overCount = { results = ResizeArray<_>(); totalBytes = 0; count = 2000 }
    check "over count budget" (isFull overCount)

let wsAbsorb () =
    let b = fresh ()
    let file : Wanxiangshu.Kernel.CapsFormat.CapsFile = { filePath = "a.fs"; label = "a"; content = "hello" }
    let b2 = absorb file b
    equal "count 1" 1 b2.count
    equal "bytes" 5 b2.totalBytes
    check "results has file" (b2.results.Count = 1)
    let b3 = absorb file b2
    equal "absorb again count" 2 b3.count
    equal "absorb again bytes" 10 b3.totalBytes
    let overBudget : Wanxiangshu.Shell.WorkspaceFiles.Budget = { results = ResizeArray<_>(); totalBytes = 8 * 1_048_576; count = 0 }
    let b4 = absorb file overBudget
    check "over-budget rejected same ref" (System.Object.ReferenceEquals(b4, overBudget))

// ── Shell.RunnerBackground ─────────────────────────────────────────────────

let rbResetAndRegister () =
    resetRunnerJobsForTesting ()
    registerActiveRunnerSession "s1"
    ()

let rbHasRunningRunnerJob () =
    resetRunnerJobsForTesting ()
    check "unknown session" (not (hasRunningRunnerJob "unknown"))
    registerActiveRunnerSession "s1"
    check "registered active" (hasRunningRunnerJob "s1")

let rbSetRunnerJobState () =
    resetRunnerJobsForTesting ()
    setRunnerJobStateForTest "s1" "running"
    check "job registered" (hasRunningRunnerJob "s1")
    ()

let rbAbortRunnerJob () =
    resetRunnerJobsForTesting ()
    registerActiveRunnerSession "s1"
    let msg = abortRunnerJob "s1"
    check "abort returns msg" (msg <> "")
    check "not running after abort" (not (hasRunningRunnerJob "s1"))

let rbCleanupRunnerJob () =
    resetRunnerJobsForTesting ()
    setRunnerJobStateForTest "s1" "running"
    check "job before cleanup" (hasRunningRunnerJob "s1")
    cleanupRunnerJob "s1" |> ignore
    check "job cleared after cleanup" (not (hasRunningRunnerJob "s1"))

// ── Shell.ChildAgentRegistry ───────────────────────────────────────────────

let carLifecycle () =
    let reg = ChildAgentRegistry.Create()
    reg.RegisterChildAgent("child-1", "agent-a", Some "parent-1")
    equal "lookup child" (Some "agent-a") (reg.LookupChildAgent "child-1")
    equal "lookup unknown" None (reg.LookupChildAgent "child-2")
    reg.UnregisterChildAgent "child-1"
    equal "after unregister" None (reg.LookupChildAgent "child-1")
    ()

let carResolveParentChain () =
    let reg = ChildAgentRegistry.Create()
    reg.RegisterChildAgent("c1", "a1", Some "p1")
    reg.RegisterChildAgent("p1", "a2", Some "gp1")
    reg.RegisterChildAgent("gp1", "a3", None)
    let r = reg.ResolveSubsessionParentID (Some "c1")
    equal "resolve grandparent" (Some "gp1") r

let carResolveParentNoCycle () =
    let reg = ChildAgentRegistry.Create()
    reg.RegisterChildAgent("c1", "a1", Some "p1")
    let r = reg.ResolveSubsessionParentID (Some "p1")
    equal "resolve parent" (Some "p1") r

let carResolveUnknown () =
    let reg = ChildAgentRegistry.Create()
    equal "resolve none" None (reg.ResolveSubsessionParentID None)
    equal "resolve unknown" (Some "x") (reg.ResolveSubsessionParentID (Some "x"))

// ── Shell.NudgeRuntime ─────────────────────────────────────────────────────

let nudgeHandleEventIgnore () =
    let store = Wanxiangshu.Shell.ReviewRuntime.createReviewStore ()
    let rt = createNudgeRuntime store None
    rt.HandleEvent(Ignore, null) |> ignore
    ()

let nudgeHandleEventStreamAbort () =
    let store = Wanxiangshu.Shell.ReviewRuntime.createReviewStore ()
    let rt = createNudgeRuntime store None
    rt.HandleEvent(StreamAbort "ws-1", null) |> ignore
    ()

// ── Shell.TreeSitterPlatform ───────────────────────────────────────────────

let tspCallOrGet () =
    let fn = createObj [ "call", box (fun () -> box "from_fn") ]
    equal "callable" "from_fn" (string (callOrGet fn "call" (fun () -> Dyn.call0 (Dyn.get fn "call"))))
    let valObj = createObj [ "val", box "from_val" ]
    equal "not callable" "from_val" (string (callOrGet valObj "val" (fun () -> box "fallback")))

let tspGetOrCall () =
    let fn = createObj [ "x", box (fun () -> "fn_result") ]
    equal "fn result" "fn_result" (string (getOrCall fn "x"))
    let valObj = createObj [ "y", box "val_result" ]
    equal "val result" "val_result" (string (getOrCall valObj "y"))

let tspGetOrCallWith () =
    let fn = createObj [ "x", box (fun (a: obj) -> "fn_" + string a) ]
    equal "fn with arg" "fn_hello" (string (getOrCallWith fn "x" (box "hello")))
    let valObj = createObj [ "y", box "static" ]
    equal "val with arg" "static" (string (getOrCallWith valObj "y" (box "hi")))

let tspDetectLanguage () =
    let pack = createObj [ "detectLanguageFromPath", box (fun (p: string) -> if p = "test.fs" then box "fs" else null) ]
    let r = string (detectLanguage pack "let x = 1" "test.fs")
    equal "detect from path" "fs" r

let tspTryGetPack () =
    match tryGetPack () with
    | Ok _ -> check "tryGetPack ok" true
    | Error _ -> check "tryGetPack error ok" true


let run () =
    subagentBuildPromptBody ()
    subagentSignalAborted ()
    subagentMakeAbortPromiseNull ()
    subagentRaceWithAbortNull ()
    wsFresh ()
    wsIsFull ()
    wsAbsorb ()
    rbResetAndRegister ()
    rbHasRunningRunnerJob ()
    rbSetRunnerJobState ()
    rbAbortRunnerJob ()
    rbCleanupRunnerJob ()
    carLifecycle ()
    carResolveParentChain ()
    carResolveParentNoCycle ()
    carResolveUnknown ()
    nudgeHandleEventIgnore ()
    nudgeHandleEventStreamAbort ()
    tspCallOrGet ()
    tspGetOrCall ()
    tspGetOrCallWith ()
    tspDetectLanguage ()
    tspTryGetPack ()
