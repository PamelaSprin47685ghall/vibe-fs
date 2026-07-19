module Wanxiangshu.Tests.RuntimeScopeLifecycleTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.RuntimeScopeForgetSession
open Wanxiangshu.Runtime.FuzzyIteratorStore
open Wanxiangshu.Runtime.ContextBudgetStore
open Wanxiangshu.Runtime.RunnerBackground
open Wanxiangshu.Runtime.SembleSearch
open Wanxiangshu.Kernel.FuzzyQuery

let private populateSession (scope: RuntimeScope) (sid: string) : string =
    scope.EnqueuePerSession(sid, (fun () -> Promise.lift ())) |> Promise.start
    scope.RegisterTempFiles(sid + "\u0000prompt", [ "f" + sid ])
    scope.AddCapsFilesIfAbsent(sid + "\u0000dir", [])

    scope.GetOrLoadCapsInflight(sid + "\u0000inflight", (fun () -> Promise.lift []))
    |> ignore

    get scope sid |> ignore
    Wanxiangshu.Runtime.LivelockGuard.check scope sid "tool" "{}" "{}" |> ignore
    registerActiveRunnerSession scope sid
    markBreakpoint scope sid 1

    let fState =
        { query = "q"
          pageSize = 10
          pageIndex = 0
          externalBasePath = None }

    storeFindIterator scope.IteratorStore sid fState

let mutable private runIdCounter = 0

let private populateEventLogStore (sid: string) : unit =
    Wanxiangshu.Runtime.EventLogRuntimeStore.getStore sid |> ignore

let private verifySessionCleared (scope: RuntimeScope) (sid: string) (findId: string) : unit =
    isNone (scope.TryFindKey("contextbudget_" + sid))
    isNone (breakpointStart scope sid)
    check (sprintf "runner cleared %s" sid) (not (hasRunningRunnerJob scope sid))
    isNone (consumeFindIterator scope.IteratorStore findId)

let run () : unit =
    runIdCounter <- runIdCounter + 1
    let runId = runIdCounter
    let prefix = sprintf "rts-%d" runId
    let scope = RuntimeScope()
    let n = 1000
    let findIds = ResizeArray<string>()
    let beforeEventLogCount = Wanxiangshu.Runtime.EventLogRuntimeStore.count ()
    let beforeEventLogIds = Wanxiangshu.Runtime.EventLogRuntimeStore.ids ()

    for i in 0 .. n - 1 do
        let sid = sprintf "%s-%d" prefix i
        findIds.Add(populateSession scope sid)
        populateEventLogStore sid

    check
        "eventLogStore count before forget"
        (Wanxiangshu.Runtime.EventLogRuntimeStore.count () = beforeEventLogCount + n)

    check "locks before forget" (scope.SessionLockCount = n)
    check "temps before forget" (scope.TempFileMapCount = n)
    check "caps before forget" (scope.CapsFileCount = n)

    for i in 0 .. n - 1 do
        forgetSession scope (sprintf "%s-%d" prefix i)

    check "locks after forget" (scope.SessionLockCount = 0)
    check "temps after forget" (scope.TempFileMapCount = 0)
    check "caps after forget" (scope.CapsFileCount = 0)
    check "capsInflight after forget" (scope.CapsInflightCount = 0)

    check "eventLogStore count after forget" (Wanxiangshu.Runtime.EventLogRuntimeStore.count () = beforeEventLogCount)

    let hasNewId =
        Wanxiangshu.Runtime.EventLogRuntimeStore.ids ()
        |> List.exists (fun id -> id.StartsWith(prefix + "-"))

    check "eventLogStore ids after forget" (not hasNewId)

    for i in 0 .. n - 1 do
        verifySessionCleared scope (sprintf "%s-%d" prefix i) findIds.[i]

    match scope.TryFindKey "livelock_state" with
    | Some m ->
        let inner = unbox<Map<string, obj>> m
        check "livelock inner map empty after forget" (Map.count inner = 0)
    | None -> ()

    match scope.TryFindKey "wanxiangshu.semble_breakpoints" with
    | Some m ->
        let inner = unbox<Map<string, int>> m
        check "breakpoints inner map empty after forget" (Map.count inner = 0)
    | None -> ()
