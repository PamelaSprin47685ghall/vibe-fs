module Wanxiangshu.Tests.OmpReviewTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.OmpPluginTestsHarness
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Hosts.Omp
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.Dyn

module Dyn = Wanxiangshu.Runtime.Dyn

let private reviewStore = PluginComposition.reviewStore

let private jsUndefined: obj = emitJsExpr () "undefined"

let private notifyCapture (notifications: ResizeArray<string>) : obj =
    emitJsExpr notifications """((ns) => function (msg, kind) { ns.push(String(msg)); })($0)"""

let private setPendingReviewStateForTest
    (store: ReviewStore)
    (sessionId: string)
    (parentId: string)
    (pending: obj)
    : unit =
    store.applyReviewTaskProjection (sessionId, Some "test task")
    store.addChild (parentId, sessionId)

    store.setPendingReview (
        sessionId,
        fun kr ->
            let js =
                match kr with
                | Accepted fb ->
                    let feedbackText = String.concat "\n" fb

                    createObj
                        [ "accepted", box true
                          "feedback", (if feedbackText = "" then null else box feedbackText)
                          "terminated", null ]
                | NeedsRevision fb ->
                    createObj
                        [ "accepted", box false
                          "feedback", box (String.concat "\n" fb)
                          "terminated", null ]
                | Terminated ->
                    createObj
                        [ "accepted", box false
                          "feedback", box "Review session closed."
                          "terminated", box true ]

            emitJsExpr
                (pending, js)
                "((p, r) => { if (typeof p === 'function') p(r); else if (p && typeof p.resolve === 'function') p.resolve(r); })($0, $1)"
            |> ignore
    )

let loopInputHandledMessageAndNotify () =
    promise {
        resetPluginState ()
        let h = createPiHarness ()
        let pi = piObject h
        do! Plugin.wanxiangshuExtension pi
        let notifications = ResizeArray<string>()

        let ctx =
            createObj
                [ "sessionManager", box (createObj [ "getSessionId", box (fun () -> box "session-1") ])
                  "ui", box (createObj [ "notify", box (notifyCapture notifications) ]) ]

        let input = eventHandler h "input"

        let! result =
            emitJsExpr (input, createObj [ "text", box "/loop fix login flow" ], ctx) "Promise.resolve($0($1, $2))"
            |> unbox<JS.Promise<obj>>

        check "loop handled" (Dyn.truthy (Dyn.get result "handled"))
        check "loop notify" (notifications |> Seq.exists (fun m -> m.Contains "loop mode is active"))
        check "loop message queued" (h.messages.Count = 1)
        check "loop triggerTurn" (Dyn.truthy (Dyn.get (Dyn.get h.messages.[0] "options") "triggerTurn"))
    }

let private executeTool (tool: obj) (toolCallId: string) (params': obj) (ctx: obj) =
    let execute = Dyn.get tool "execute"

    emitJsExpr (execute, toolCallId, params', jsUndefined, jsUndefined, ctx) "Promise.resolve($0($1, $2, $3, $4, $5))"
    |> unbox<JS.Promise<obj>>

let private toolText (result: obj) : string =
    let contentRaw = Dyn.get result "content"

    if Dyn.isNullish contentRaw || not (Dyn.isArray contentRaw) then
        failwith "tool result missing content array"

    let content = unbox<obj array> contentRaw

    if content.Length = 0 then
        failwith "tool result content is empty"

    str content.[0] "text"

let returnReviewerVerdictPerfectRevise () =
    promise {
        resetPluginState ()
        let h = createPiHarness ()
        let pi = piObject h
        do! Plugin.wanxiangshuExtension pi
        let tool = h.tools |> Seq.find (fun t -> str t "name" = "return_reviewer")
        let reviewSessionId = "review-child-1"
        let task = "review loop task"
        let mutable firstKr: ReviewResult option = None
        reviewStore.applyReviewTaskProjection (reviewSessionId, Some task)
        reviewStore.setPendingReview (reviewSessionId, (fun kr -> firstKr <- Some kr))

        let ctx1 =
            createObj [ "sessionManager", box (createObj [ "getSessionId", box (fun () -> box reviewSessionId) ]) ]

        let! passResult =
            executeTool tool "call-1" (createObj [ "verdict", box "PERFECT"; "feedback", box "looks good" ]) ctx1

        check "PERFECT first pass double-check prompt" ((toolText passResult).Contains "objective =")

        let! passResult2 =
            executeTool
                tool
                "call-2"
                (createObj [ "verdict", box "PERFECT"; "feedback", box "confirmed after double-check" ])
                ctx1

        equal "PERFECT second pass verdict" (Some(Accepted [ "confirmed after double-check" ])) firstKr

        check
            "PERFECT result text is submit ack"
            ((toolText passResult2).Contains "Verdict submitted" || (toolText passResult2).Contains "stop")

        let mutable secondKr: ReviewResult option = None
        reviewStore.setPendingReview (reviewSessionId, (fun kr -> secondKr <- Some kr))

        let! rejectResult =
            executeTool tool "call-3" (createObj [ "verdict", box "REVISE"; "feedback", box "Fix it" ]) ctx1

        equal "REVISE verdict" (Some(NeedsRevision [ "Fix it" ])) secondKr

        check
            "revise result text is submit ack"
            ((toolText rejectResult).Contains "Verdict submitted" || (toolText rejectResult).Contains "stop")
    }

let private createResolvablePromise () : obj =
    emitJsExpr
        ()
        "(function() { var res; var p = new Promise(function(r) { res = r; }); return { promise: p, resolve: res }; })()"

let private testPendingPerfectPass tool ctx reviewSessionId parentSessionId =
    promise {
        let firstPending = createResolvablePromise ()
        setPendingReviewStateForTest reviewStore reviewSessionId parentSessionId firstPending

        let! passResult1 =
            executeTool tool "call-1" (createObj [ "verdict", box "PERFECT"; "feedback", box "looks good" ]) ctx

        check "PERFECT first pass double-check prompt" ((toolText passResult1).Contains "objective =")

        let! passResult =
            executeTool
                tool
                "call-1-confirm"
                (createObj [ "verdict", box "PERFECT"; "feedback", box "confirmed after double-check" ])
                ctx

        let! firstResolved = emitJsExpr firstPending "$0.promise" |> unbox<JS.Promise<obj>>
        equal "setPending PERFECT feedback present" "confirmed after double-check" (str firstResolved "feedback")

        check
            "setPending PERFECT tool text is submit ack"
            ((toolText passResult).Contains "Verdict submitted" || (toolText passResult).Contains "stop")
    }

let private testPendingRevisePass tool ctx reviewSessionId parentSessionId =
    promise {
        let secondPending = createResolvablePromise ()
        setPendingReviewStateForTest reviewStore reviewSessionId parentSessionId secondPending

        let! rejectResult =
            executeTool tool "call-2" (createObj [ "verdict", box "REVISE"; "feedback", box "Fix it" ]) ctx

        let! secondResolved = emitJsExpr secondPending "$0.promise" |> unbox<JS.Promise<obj>>
        equal "setPending REVISE feedback" "Fix it" (str secondResolved "feedback")

        check
            "setPending revise tool text is submit ack"
            ((toolText rejectResult).Contains "Verdict submitted" || (toolText rejectResult).Contains "stop")
    }

let returnReviewerViaSetPendingStateForTest () =
    promise {
        resetPluginState ()
        let h = createPiHarness ()
        let pi = piObject h
        do! Plugin.wanxiangshuExtension pi
        let tool = h.tools |> Seq.find (fun t -> str t "name" = "return_reviewer")
        let reviewSessionId = "review-child-1"
        let parentSessionId = "parent-1"

        let ctx =
            createObj [ "sessionManager", box (createObj [ "getSessionId", box (fun () -> box reviewSessionId) ]) ]

        do! testPendingPerfectPass tool ctx reviewSessionId parentSessionId
        do! testPendingRevisePass tool ctx reviewSessionId parentSessionId
    }
