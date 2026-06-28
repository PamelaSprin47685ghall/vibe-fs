module Wanxiangshu.Tests.OmpPluginCoreTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.Dyn
module Dyn = Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.FallbackEventBridge
open Wanxiangshu.Omp.PluginCore

/// Verify the reviewStore singleton is wired into the CoreServices so the
/// registered tools see the same instance that tests manipulate.
let reviewStoreIsSharedSingleton () =
    let s1 = reviewStore
    let s2 = reviewStore
    check "reviewStore singleton reference equality" (System.Object.ReferenceEquals(s1, s2))
    let sessionId = "core-services-1"
    s1.activateReview(sessionId, "task", 0L)
    check "shared store sees activation" (s1.isReviewActive sessionId)
    check "shared store sees activation via second reference" (s2.isReviewActive sessionId)
    s1.deactivateReview sessionId
    check "deactivation observed by second reference" (not (s2.isReviewActive sessionId))

let clearReviewStatesNoError () =
    let s = reviewStore
    s.activateReview("core-svc-2", "x", 0L)
    s.clearReviewSessions ()
    check "clearReviewSessions clears" (not (s.isReviewActive "core-svc-2"))

/// Minimal `pi` harness capturing `pi.on(event, handler)` registrations so a
/// test can invoke the captured closure and observe its side effects.
let private capturePi () : obj * (unit -> obj array) =
    let handlers : ResizeArray<obj> = ResizeArray ()
    let hookStore =
        createObj [ "events", box (createObj [ "event", box handlers ]) ]
    let pi =
        emitJsExpr hookStore
            "((hs) => ({ on(event, handler) { if (!hs.events[event]) hs.events[event] = []; hs.events[event].push(handler); } }))($0)"
        |> unbox<obj>
    let getHandlers () : obj array =
        let raw =
            emitJsExpr hookStore "((hs) => hs.events['event'] || [])($0)"
            |> unbox<obj>
        if Dyn.isNullish raw || not (Dyn.isArray raw) then [||] else unbox<obj array> raw
    pi, getHandlers

let private makeFakeRuntime () : FallbackRuntimeState =
    FallbackRuntimeState()

let private makeFakeConfig () : FallbackConfig =
    { DefaultChain = [ { ProviderID = "oai"; ModelID = "gpt5"; Variant = None; Temperature = None; TopP = None; MaxTokens = None; ReasoningEffort = None; Thinking = false } ]
      AgentChains = Map.ofList []
      MaxRetries = 2
      LoopMaxContinues = 3 }

let private invokeFallbackHandler (handler: obj) (event: obj) (ctx: obj) : unit =
    emitJsExpr (handler, event, ctx) "(($0)($1, $2))" |> ignore

/// Drive the registered abort handler with `evtType` against a sessionId
/// resolved through a `sessionManager.getSessionId` ctx, and assert review
/// state matches `expectActive`.
let private driveAbort (evtType: string) (sessionId: string) (expectActive: bool) : unit =
    let sessionMgr =
        createObj [
            "getSessionId", box(fun () -> box sessionId)
        ]
    let ctx = createObj [ "sessionManager", box sessionMgr ]
    let event = createObj [ "type", box evtType ]
    let pi, getHandlers = capturePi ()
    registerAbortHandler pi reviewStore None
    let handlers = getHandlers ()
    check $"{evtType} captured exactly one handler" (handlers.Length = 1)
    let handler = handlers.[0]
    emitJsExpr (handler, event, ctx) "(($0)($1, $2))" |> ignore
    let actual = reviewStore.isReviewActive sessionId
    check $"{evtType} leaves review inactive for {sessionId}" (actual = expectActive)

/// `session.abort` must deactivate the active review for the host-reported
/// sessionId. Without this hook the review state would survive a host-driven
/// abort and re-emerge as if the next session owned it.
let abortHookDeactivatesReview () =
    let sid = "abort-hook-sid-1"
    reviewStore.activateReview(sid, "task", 0L)
    check "precondition: review active" (reviewStore.isReviewActive sid)
    driveAbort "session.abort" sid false

/// `stream.abort` must mirror the session.abort path: review state clears.
let streamAbortHookDeactivatesReview () =
    let sid = "stream-abort-hook-sid-1"
    reviewStore.activateReview(sid, "task", 0L)
    check "precondition: review active" (reviewStore.isReviewActive sid)
    driveAbort "stream.abort" sid false

/// `session.error` collapses to the same outcome as aborts.
let sessionErrorHookDeactivatesReview () =
    let sid = "session-error-hook-sid-1"
    reviewStore.activateReview(sid, "task", 0L)
    check "precondition: review active" (reviewStore.isReviewActive sid)
    driveAbort "session.error" sid false

/// Events outside the abort/error set must be ignored: review state stays put.
let unrelatedEventLeavesReviewActive () =
    let sid = "unrelated-event-sid-1"
    reviewStore.activateReview(sid, "task", 0L)
    check "precondition: review active" (reviewStore.isReviewActive sid)
    driveAbort "session.idle" sid true

/// OMP session.error must be routed through the fallback handler before
/// review cleanup. Previously the translator looked for `props.error`, which
/// was never set, so the event bypassed fallback entirely.
let ompErrorEventRoutesToFallback () =
    let sessionMgr = createObj [ "getSessionId", box(fun () -> box "omp-fb-error-sid") ]
    let ctx = createObj [ "sessionManager", box sessionMgr ]
    let event = createObj [
        "type", box "session.error"
        "error", box(createObj [ "name", box "APIError"; "message", box "rate limit"; "statusCode", box "429"; "isRetryable", box "true" ])
    ]
    let pi, getHandlers = capturePi ()
    let mutable handlerCalled = false
    let runtime = makeFakeRuntime ()
    let fakeHandler (rawEvent: obj) : JS.Promise<FallbackHookResult> =
        handlerCalled <- true
        Promise.lift { Consumed = false; State = runtime.GetOrCreateState "omp-fb-error-sid" }
    registerAbortHandler pi reviewStore (Some fakeHandler)
    let handlers : obj array = getHandlers ()
    check "exactly one handler registered" (handlers.Length = 1)
    invokeFallbackHandler handlers.[0] event ctx
    check "fallback handler saw session.error" handlerCalled

/// Non-terminal OMP events (session.idle) must also reach the fallback
/// handler so scan completion / CheckTodoState can fire.
let ompIdleEventRoutesToFallback () =
    let sessionMgr = createObj [ "getSessionId", box(fun () -> box "omp-fb-idle-sid") ]
    let ctx = createObj [ "sessionManager", box sessionMgr ]
    let event = createObj [ "type", box "session.idle" ]
    let pi, getHandlers = capturePi ()
    let mutable handlerCalled = false
    let runtime = makeFakeRuntime ()
    let fakeHandler (rawEvent: obj) : JS.Promise<FallbackHookResult> =
        handlerCalled <- true
        Promise.lift { Consumed = false; State = runtime.GetOrCreateState "omp-fb-idle-sid" }
    registerAbortHandler pi reviewStore (Some fakeHandler)
    let handlers : obj array = getHandlers ()
    invokeFallbackHandler handlers.[0] event ctx
    check "fallback handler saw session.idle" handlerCalled

let run () =
    reviewStoreIsSharedSingleton ()
    clearReviewStatesNoError ()
    abortHookDeactivatesReview ()
    streamAbortHookDeactivatesReview ()
    sessionErrorHookDeactivatesReview ()
    unrelatedEventLeavesReviewActive ()
    ompErrorEventRoutesToFallback ()
    ompIdleEventRoutesToFallback ()
