module Wanxiangshu.Tests.OmpPluginCoreTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.Dyn

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.GateTransitions
open Wanxiangshu.Runtime.Fallback.FallbackEventBridge
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Hosts.Omp.PluginComposition
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Runtime.SubsessionActorRegistry

/// Verify the reviewStore singleton is wired into the CoreServices so the
/// registered tools see the same instance that tests manipulate.
let reviewStoreIsSharedSingleton () =
    let s1 = reviewStore
    let s2 = reviewStore
    check "reviewStore singleton reference equality" (System.Object.ReferenceEquals(s1, s2))
    let sessionId = "core-services-1"
    s1.applyReviewTaskProjection (sessionId, Some "task")
    check "shared store sees activation" (s1.getReviewTask sessionId = Some "task")
    check "shared store sees activation via second reference" (s2.getReviewTask sessionId = Some "task")
    s1.applyReviewTaskProjection (sessionId, None)
    check "deactivation observed by second reference" (s2.getReviewState sessionId |> Option.isNone)

let clearReviewStatesNoError () =
    let s = reviewStore
    s.applyReviewTaskProjection ("core-svc-2", Some "x")
    s.clearReviewSessions ()
    check "clearReviewSessions clears" (s.getReviewState "core-svc-2" |> Option.isNone)

/// Minimal `pi` harness capturing `pi.on(event, handler)` registrations so a
/// test can invoke the captured closure and observe its side effects.
let private capturePi () : obj * (unit -> obj array) =
    let handlers: ResizeArray<obj> = ResizeArray()
    let hookStore = createObj [ "events", box (createObj [ "event", box handlers ]) ]

    let pi =
        emitJsExpr
            hookStore
            "((hs) => ({ on(event, handler) { if (!hs.events[event]) hs.events[event] = []; hs.events[event].push(handler); } }))($0)"
        |> unbox<obj>

    let getHandlers () : obj array =
        let raw =
            emitJsExpr hookStore "((hs) => hs.events['event'] || [])($0)" |> unbox<obj>

        if Dyn.isNullish raw || not (Dyn.isArray raw) then
            [||]
        else
            unbox<obj array> raw

    pi, getHandlers

let private makeFakeRuntime () : FallbackRuntimeStore = FallbackRuntimeStore()

let private makeFakeConfig () : FallbackConfig =
    { DefaultChain =
        [ { ProviderID = "oai"
            ModelID = "gpt5"
            Variant = None
            Temperature = None
            TopP = None
            MaxTokens = None
            ReasoningEffort = None
            Thinking = false } ]
      AgentChains = Map.ofList []
      MaxRetries = 2
      LoopMaxContinues = 3
      MaxRecoveries = 5 }

let private invokeFallbackHandler (handler: obj) (event: obj) (ctx: obj) : unit =
    emitJsExpr (handler, event, ctx) "(($0)($1, $2))" |> ignore

/// Drive the registered abort handler with `evtType` against a sessionId
/// resolved through a `sessionManager.getSessionId` ctx, and assert review
/// state matches `expectActive`.
let private driveAbort
    (store: ReviewStore)
    (evtType: string)
    (sessionId: string)
    (expectActive: bool)
    : JS.Promise<unit> =
    promise {
        let! root = mkdtempAsync "omp-abort-hook-"
        let sessionMgr = createObj [ "getSessionId", box (fun () -> box sessionId) ]

        let ctx = createObj [ "sessionManager", box sessionMgr; "cwd", box root ]
        let event = createObj [ "type", box evtType ]
        let pi, getHandlers = capturePi ()
        let runtime = FallbackRuntimeStore()
        registerAbortHandler pi store runtime None
        let handlers = getHandlers ()
        check $"{evtType} captured exactly one handler" (handlers.Length = 1)
        let handler = handlers.[0]

        let! _ =
            emitJsExpr (handler, event, ctx) "Promise.resolve($0($1, $2))"
            |> unbox<JS.Promise<obj>>

        let actual = store.getReviewState sessionId |> Option.isSome
        check $"{evtType} leaves review inactive for {sessionId}" (actual = expectActive)
    }

/// `session.abort` must deactivate the active review for the host-reported
/// sessionId. Without this hook the review state would survive a host-driven
/// abort and re-emerge as if the next session owned it.
let abortHookDeactivatesReview () =
    promise {
        let localStore = createReviewStore ()
        let sid = "abort-hook-sid-1"
        localStore.applyReviewTaskProjection (sid, Some "task")
        check "precondition: review active" (localStore.getReviewTask sid = Some "task")

        Wanxiangshu.Runtime.RunnerBackground.registerActiveRunnerSession
            Wanxiangshu.Hosts.Omp.ExecutorTools.ompScope
            sid

        check
            "precondition: has running runner job"
            (Wanxiangshu.Runtime.RunnerBackground.hasRunningRunnerJob Wanxiangshu.Hosts.Omp.ExecutorTools.ompScope sid)

        do! driveAbort localStore "session.abort" sid false

        check
            "postcondition: runner job was aborted"
            (not (
                Wanxiangshu.Runtime.RunnerBackground.hasRunningRunnerJob
                    Wanxiangshu.Hosts.Omp.ExecutorTools.ompScope
                    sid
            ))
    }

/// `stream.abort` must mirror the session.abort path: review state clears.
let streamAbortHookDeactivatesReview () =
    promise {
        let localStore = createReviewStore ()
        let sid = "stream-abort-hook-sid-1"
        localStore.applyReviewTaskProjection (sid, Some "task")
        check "precondition: review active" (localStore.getReviewTask sid = Some "task")

        Wanxiangshu.Runtime.RunnerBackground.registerActiveRunnerSession
            Wanxiangshu.Hosts.Omp.ExecutorTools.ompScope
            sid

        check
            "precondition: has running runner job"
            (Wanxiangshu.Runtime.RunnerBackground.hasRunningRunnerJob Wanxiangshu.Hosts.Omp.ExecutorTools.ompScope sid)

        do! driveAbort localStore "stream.abort" sid false

        check
            "postcondition: runner job was aborted"
            (not (
                Wanxiangshu.Runtime.RunnerBackground.hasRunningRunnerJob
                    Wanxiangshu.Hosts.Omp.ExecutorTools.ompScope
                    sid
            ))
    }

/// `session.error` collapses to the same outcome as aborts.
let sessionErrorHookDeactivatesReview () =
    promise {
        let localStore = createReviewStore ()
        let sid = "session-error-hook-sid-1"
        localStore.applyReviewTaskProjection (sid, Some "task")
        check "precondition: review active" (localStore.getReviewTask sid = Some "task")
        do! driveAbort localStore "session.error" sid false
    }

/// Events outside the abort/error set must be ignored: review state stays put.
let unrelatedEventLeavesReviewActive () =
    promise {
        let localStore = createReviewStore ()
        let sid = "unrelated-event-sid-1"
        localStore.applyReviewTaskProjection (sid, Some "task")
        check "precondition: review active" (localStore.getReviewTask sid = Some "task")
        do! driveAbort localStore "session.idle" sid true
    }

/// OMP session.error must be routed through the fallback handler before
/// review cleanup. Previously the translator looked for `props.error`, which
/// was never set, so the event bypassed fallback entirely.
let ompErrorEventRoutesToFallback () =
    let sessionMgr =
        createObj [ "getSessionId", box (fun () -> box "omp-fb-error-sid") ]

    let ctx = createObj [ "sessionManager", box sessionMgr ]

    let event =
        createObj
            [ "type", box "session.error"
              "error",
              box (
                  createObj
                      [ "name", box "APIError"
                        "message", box "rate limit"
                        "statusCode", box "429"
                        "isRetryable", box "true" ]
              ) ]

    let pi, getHandlers = capturePi ()
    let mutable handlerCalled = false
    let runtime = makeFakeRuntime ()

    let fakeHandler (rawEvent: obj) : JS.Promise<FallbackHookResult> =
        handlerCalled <- true

        Promise.lift
            { Consumed = false
              State = runtime.GetOrCreateState "omp-fb-error-sid" }

    registerAbortHandler pi reviewStore runtime (Some fakeHandler)
    let handlers: obj array = getHandlers ()
    check "exactly one handler registered" (handlers.Length = 1)
    invokeFallbackHandler handlers.[0] event ctx
    check "fallback handler saw session.error" handlerCalled

/// Non-terminal OMP events (session.idle) must also reach the fallback
/// handler so scan completion / CheckTodoState can fire.
let ompIdleEventRoutesToFallback () =
    let sessionMgr = createObj [ "getSessionId", box (fun () -> box "omp-fb-idle-sid") ]
    let ctx = createObj [ "sessionManager", box sessionMgr ]
    let event = createObj [ "type", box "session.idle" ]
    let pi, getHandlers = capturePi ()
    let mutable handlerCalled = false
    let runtime = makeFakeRuntime ()

    let fakeHandler (rawEvent: obj) : JS.Promise<FallbackHookResult> =
        handlerCalled <- true

        Promise.lift
            { Consumed = false
              State = runtime.GetOrCreateState "omp-fb-idle-sid" }

    registerAbortHandler pi reviewStore runtime (Some fakeHandler)
    let handlers: obj array = getHandlers ()
    invokeFallbackHandler handlers.[0] event ctx
    check "fallback handler saw session.idle" handlerCalled

/// Verify that when reconcileUnfinishedRuns runs on startup, it constructs a real hostFactory
/// and triggers physical close (sessionDelete) on zombie sessions.
let ompReconciliationClosesZombieSessions () =
    promise {
        let! root = mkdtempAsync "omp-reconcile-test-"
        let sid = SessionId.create "omp-zombie-sid-1"
        let parent = SessionId.create "parent-sid"
        let rid = RunId.create "run-zombie-1"

        let eventStore = Wanxiangshu.Runtime.SubsessionEventStore.create root

        let runStartedData =
            { SessionId = sid
              ParentSessionId = parent
              RunId = rid }

        do! eventStore.Append(sid, [ RunStarted runStartedData ])

        let mutable sessionDeleteCalled = false
        let mutable calledSessionId = ""

        let sessionApi =
            createObj
                [ "sessionDelete",
                  box (fun (arg: obj) ->
                      sessionDeleteCalled <- true
                      calledSessionId <- Dyn.str arg "sessionId"
                      Promise.lift (box null)) ]

        let pi = createObj [ "directory", box root; "session", box sessionApi ]

        let hostFactory (s: string) =
            Wanxiangshu.Hosts.Omp.SubsessionHostAdapter.createHost null "" pi

        let! _ = Wanxiangshu.Runtime.SubsessionReconcile.reconcileUnfinishedRuns root (Some hostFactory)

        check "sessionDelete was called" sessionDeleteCalled
        check "sessionDelete called with correct sessionId" (calledSessionId = "omp-zombie-sid-1")

        let actor =
            Wanxiangshu.Runtime.SubsessionActorRegistry.SubsessionActorRegistry.GetOrCreate
                root
                "omp-zombie-sid-1"
                (hostFactory "omp-zombie-sid-1")
                eventStore

        check "actor state is poisoned after reconcile" actor.IsPoisoned
    }

let run () =
    promise {
        reviewStoreIsSharedSingleton ()
        clearReviewStatesNoError ()
        do! abortHookDeactivatesReview ()
        do! streamAbortHookDeactivatesReview ()
        do! sessionErrorHookDeactivatesReview ()
        do! unrelatedEventLeavesReviewActive ()
        ompErrorEventRoutesToFallback ()
        ompIdleEventRoutesToFallback ()
        do! ompReconciliationClosesZombieSessions ()
    }
