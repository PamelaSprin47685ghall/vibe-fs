module Wanxiangshu.Tests.OmpReviewTests
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.OmpPluginTestsHarness
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Omp.ChildSession
open Wanxiangshu.Omp.PiResolve
open Wanxiangshu.Omp.Plugin
open Wanxiangshu.Omp.ReviewLoop
open Wanxiangshu.Omp.ReviewTools
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.RunnerBackground
open Wanxiangshu.Shell.Dyn
module Dyn = Wanxiangshu.Shell.Dyn

let private jsUndefined : obj = emitJsExpr () "undefined"
let private notifyCapture (notifications: ResizeArray<string>) : obj =
    emitJsExpr notifications
        """((ns) => function (msg, kind) { ns.push(String(msg)); })($0)"""

let loopInputHandledMessageAndNotify () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    let notifications = ResizeArray<string>()
    let ctx =
        createObj [
            "sessionManager", box(createObj [ "getSessionId", box(fun () -> box "session-1") ])
            "ui", box(createObj [ "notify", box(notifyCapture notifications) ])
        ]
    let input = eventHandler h "input"
    let! result =
        emitJsExpr (input, createObj [ "text", box "/loop fix login flow" ], ctx)
            "Promise.resolve($0($1, $2))"
        |> unbox<JS.Promise<obj>>
    check "loop handled" (Dyn.truthy (Dyn.get result "handled"))
    check "loop notify" (notifications |> Seq.exists (fun m -> m.Contains "loop mode is active"))
    check "loop message queued" (h.messages.Count = 1)
    let opts = Dyn.get h.messages.[0] "options"
    check "loop triggerTurn" (Dyn.truthy (Dyn.get opts "triggerTurn"))
}

let private executeTool (tool: obj) (toolCallId: string) (params': obj) (ctx: obj) =
    let execute = Dyn.get tool "execute"
    emitJsExpr (execute, toolCallId, params', jsUndefined, jsUndefined, ctx)
        "Promise.resolve($0($1)($2)($3)($4)($5))"
    |> unbox<JS.Promise<obj>>

let private toolText (result: obj) : string =
    let contentRaw = Dyn.get result "content"
    if Dyn.isNullish contentRaw || not (Dyn.isArray contentRaw) then
        failwith "tool result missing content array"
    let content = unbox<obj array> contentRaw
    if content.Length = 0 then failwith "tool result content is empty"
    str content.[0] "text"

let returnReviewerVerdictPerfectRevise () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    let tool = h.tools |> Seq.find (fun t -> str t "name" = "return_reviewer")
    let reviewSessionId = "review-child-1"
    let task = "review loop task"
    let ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    let mutable firstKr : ReviewResult option = None
    reviewStore.activateReview(reviewSessionId, task, ts)
    reviewStore.setPendingReview(reviewSessionId, fun kr -> firstKr <- Some kr)
    let ctx1 = createObj [ "sessionManager", box(createObj [ "getSessionId", box(fun () -> box reviewSessionId) ]) ]
    let! passResult =
        executeTool tool "call-1" (createObj [ "verdict", box "PERFECT" ]) ctx1
    equal "PERFECT verdict" (Some (Accepted "")) firstKr
    equal "PERFECT result text" "Review submitted: accepted." (toolText passResult)
    let mutable secondKr : ReviewResult option = None
    reviewStore.setPendingReview(reviewSessionId, fun kr -> secondKr <- Some kr)
    let! rejectResult =
        executeTool tool "call-2" (createObj [ "verdict", box "REVISE"; "feedback", box "Fix it" ]) ctx1
    equal "REVISE verdict" (Some (NeedsRevision "Fix it")) secondKr
    equal "revise result text" "Review submitted: revision requested with feedback." (toolText rejectResult)
}

let returnReviewerViaSetPendingStateForTest () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    let tool = h.tools |> Seq.find (fun t -> str t "name" = "return_reviewer")
    let reviewSessionId = "review-child-1"
    let parentSessionId = "parent-1"
    let ctx =
        createObj [
            "sessionManager", box(createObj [ "getSessionId", box(fun () -> box reviewSessionId) ])
        ]
    let firstPending = emitJsExpr () "Promise.withResolvers()" |> unbox<obj>
    emitJsExpr (_test, reviewSessionId, parentSessionId, firstPending)
        """$0.setPendingReviewStateForTest($1)($2)($3)"""
        |> ignore
    let! passResult =
        executeTool tool "call-1" (createObj [ "verdict", box "PERFECT" ]) ctx
    let! firstResolved =
        emitJsExpr firstPending "$0.promise"
        |> unbox<JS.Promise<obj>>
    equal "setPending PERFECT feedback absent" true (Dyn.isNullish (Dyn.get firstResolved "feedback"))
    equal "setPending PERFECT tool text" "Review submitted: accepted." (toolText passResult)
    let secondPending = emitJsExpr () "Promise.withResolvers()" |> unbox<obj>
    emitJsExpr (_test, reviewSessionId, parentSessionId, secondPending)
        """$0.setPendingReviewStateForTest($1)($2)($3)"""
        |> ignore
    let! rejectResult =
        executeTool tool "call-2" (createObj [ "verdict", box "REVISE"; "feedback", box "Fix it" ]) ctx
    let! secondResolved =
        emitJsExpr secondPending "$0.promise"
        |> unbox<JS.Promise<obj>>
    equal "setPending REVISE feedback" "Fix it" (str secondResolved "feedback")
    equal "setPending revise tool text" "Review submitted: revision requested with feedback." (toolText rejectResult)
}

let runReviewLoopChildToolNames () = promise {
    clearCodingAgentModuleForTest ()
    let captured = ref [||]
    let store = createReviewStore ()
    let childId = "review-child-tools"
    setCodingAgentModuleForTest (
        createObj [
            "SessionManager",
                box(
                    createObj [
                        "create", box(fun (_cwd: string) -> createObj [ "getSessionId", box(fun () -> box "sm-1") ])
                    ])
        ])
    let promptAcceptOnFirst =
        box(fun (_: obj) ->
            store.resolvePendingReview(childId, Accepted "") |> ignore
            emitJsExpr () "Promise.resolve()"
            |> unbox<JS.Promise<unit>>)
    let createAgentSession =
        box(fun (body: obj) ->
            captured.Value <- unbox<string array> (Dyn.get body "toolNames")
            emitJsExpr promptAcceptOnFirst
                """Promise.resolve({
                    session: {
                        sessionManager: { getSessionId: () => 'review-child-tools' },
                        prompt: (msg) => $0(msg),
                        waitForIdle: () => Promise.resolve(),
                        abort: () => {}
                    },
                    dispose: null
                })"""
            |> unbox<JS.Promise<obj>>)
    let pi = createObj [ "pi", box(createObj [ "createAgentSession", createAgentSession ]) ]
    let ctx = createObj [ "cwd", box "/tmp/ws" ]
    let! _ =
        runReviewLoop pi ctx store "parent-tools" "report" [||] (Some "task")
    equal "runReviewLoop tool count" ompReviewChildToolNames.Length captured.Value.Length
    for i in 0 .. ompReviewChildToolNames.Length - 1 do
        equal ("runReviewLoop tool " + string i) ompReviewChildToolNames.[i] captured.Value.[i]
}

let runReviewLoopAcceptsWhenPendingResolved () = promise {
    clearCodingAgentModuleForTest ()
    let childId = "review-child-accept"
    let store = createReviewStore ()
    let promptResolve =
        box(fun (_: obj) ->
            store.resolvePendingReview(childId, Accepted "") |> ignore
            emitJsExpr () "Promise.resolve()"
            |> unbox<JS.Promise<unit>>)
    let session =
        createObj [
            "sessionManager", box(createObj [ "getSessionId", box(fun () -> box childId) ])
            "prompt", promptResolve
            "waitForIdle", box(fun () -> emitJsExpr () "Promise.resolve()" |> unbox<JS.Promise<unit>>)
            "abort", box(fun () -> ())
        ]
    let createAgentSession =
        box(fun (_body: obj) ->
            emitJsExpr session
                """Promise.resolve({ session: $0, dispose: null })"""
            |> unbox<JS.Promise<obj>>)
    let pi = createObj [ "pi", box(createObj [ "createAgentSession", createAgentSession ]) ]
    setCodingAgentModuleForTest (
        createObj [
            "SessionManager",
                box(
                    createObj [
                        "create", box(fun (_cwd: string) -> createObj [ "getSessionId", box(fun () -> box "sm-1") ])
                    ])
        ])
    let ctx = createObj [ "cwd", box "/tmp/ws" ]
    let! outcome = runReviewLoop pi ctx store "parent-accept" "report body" [| "src/a.fs" |] (Some "fix")
    check "accepted no feedback" outcome.feedback.IsNone
    check "accepted not terminated" (not (defaultArg outcome.terminated false))
    check "accepted flag" (defaultArg outcome.accepted false)
}


