module VibeFs.Tests.OmpReviewTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.OmpSessionTools
open VibeFs.Kernel.ReviewSession
open VibeFs.Omp.ChildSession
open VibeFs.Omp.PiResolve
open VibeFs.Omp.Plugin
open VibeFs.Omp.ReviewLoop
open VibeFs.Omp.ReviewTools
open VibeFs.Shell.ReviewRuntime
open VibeFs.Shell.RunnerBackground
open VibeFs.Shell.Dyn
module Dyn = VibeFs.Shell.Dyn

type private PiHarness =
    { hookStore: obj
      tools: ResizeArray<obj>
      messages: ResizeArray<obj> }

let private createHarness () : PiHarness =
    let tools = ResizeArray<obj>()
    let messages = ResizeArray<obj>()
    let hookStore =
        createObj [
            "tools", box tools
            "commands", box (ResizeArray<obj>())
            "messages", box messages
            "events", box(createObj [])
            "activeTools", box [| "coder"; "submit_review"; "return_reviewer" |]
        ]
    { hookStore = hookStore; tools = tools; messages = messages }

let private piObject (h: PiHarness) : obj =
    let tb =
        createObj [
            "Type",
                box(
                    createObj [
                        "Object", box(fun (p: obj) -> createObj [ "type", box "object"; "properties", box p ])
                        "String", box(fun (o: obj) -> createObj [ "type", box "string" ])
                        "Number", box(fun (o: obj) -> createObj [ "type", box "number" ])
                        "Boolean", box(fun (o: obj) -> createObj [ "type", box "boolean" ])
                        "Null", box(fun (_: obj) -> createObj [ "type", box "null" ])
                        "Union", box(fun (items: obj array) -> createObj [ "anyOf", box items ])
                        "Enum", box(fun (values: obj array) (o: obj) -> createObj [ "type", box "enum"; "values", box values ])
                        "Array", box(fun (items: obj) -> createObj [ "type", box "array"; "items", box items ])
                        "Optional", box(fun (schema: obj) -> schema)
                    ])
        ]
    let pi =
        emitJsExpr h.hookStore
            """((hs) => ({
        on(event, handler) {
            if (!hs.events[event]) hs.events[event] = [];
            hs.events[event].push(handler);
        },
        registerTool(tool) { hs.tools.push(tool); },
        registerCommand(name, config) {},
        sendMessage(message, options) { hs.messages.push({ message, options }); },
        getActiveTools() { return hs.activeTools; },
        setActiveTools(names) { hs.activeTools = names; return Promise.resolve(); }
    }))($0)"""
        |> unbox<obj>
    pi?("typebox") <- tb
    pi

let private handler (h: PiHarness) (event: string) : obj =
    let handlers = Dyn.get (Dyn.get h.hookStore "events") event
    unbox<obj array> handlers |> Array.head

let private jsUndefined : obj = emitJsExpr () "undefined"

let private notifyCapture (notifications: ResizeArray<string>) : obj =
    emitJsExpr notifications
        """((ns) => function (msg, kind) { ns.push(String(msg)); })($0)"""

let private resetReview () =
    resetOmpPluginTestState ()

let loopInputHandledMessageAndNotify () = promise {
    resetReview ()
    let h = createHarness ()
    let pi = piObject h
    do! kunweiExtension pi
    let notifications = ResizeArray<string>()
    let ctx =
        createObj [
            "sessionManager", box(createObj [ "getSessionId", box(fun () -> box "session-1") ])
            "ui", box(createObj [ "notify", box(notifyCapture notifications) ])
        ]
    let input = handler h "input"
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

let private findReturnReviewer (h: PiHarness) : obj =
    h.tools |> Seq.find (fun t -> str t "name" = "return_reviewer")

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

let returnReviewerVerdictPassReject () = promise {
    resetReview ()
    let h = createHarness ()
    let pi = piObject h
    do! kunweiExtension pi
    let tool = findReturnReviewer h
    let reviewSessionId = "review-child-1"
    let task = "review loop task"
    let ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

    let mutable firstKr : ReviewResult option = None
    reviewStore.activateReview(reviewSessionId, task, ts)
    reviewStore.setPendingReview(reviewSessionId, fun kr -> firstKr <- Some kr)
    let ctx1 = createObj [ "sessionManager", box(createObj [ "getSessionId", box(fun () -> box reviewSessionId) ]) ]
    let! passResult =
        executeTool tool "call-1" (createObj [ "verdict", box "PASS" ]) ctx1
    equal "PASS verdict" (Some Accepted) firstKr
    equal "PASS result text" "Review submitted: accepted." (toolText passResult)

    let mutable secondKr : ReviewResult option = None
    reviewStore.setPendingReview(reviewSessionId, fun kr -> secondKr <- Some kr)
    let! rejectResult =
        executeTool tool "call-2" (createObj [ "verdict", box "REJECT"; "feedback", box "Fix it" ]) ctx1
    equal "REJECT verdict" (Some (Rejected "Fix it")) secondKr
    equal "reject result text" "Review submitted: rejected with feedback." (toolText rejectResult)
}

let returnReviewerViaSetPendingStateForTest () = promise {
    resetReview ()
    let h = createHarness ()
    let pi = piObject h
    do! kunweiExtension pi
    let tool = findReturnReviewer h
    let reviewSessionId = "review-child-1"
    let parentSessionId = "parent-1"
    let ctx =
        createObj [
            "sessionManager", box(createObj [ "getSessionId", box(fun () -> box reviewSessionId) ])
        ]

    let firstPending =
        emitJsExpr () "Promise.withResolvers()"
        |> unbox<obj>
    emitJsExpr (_test, reviewSessionId, parentSessionId, firstPending)
        """$0.setPendingReviewStateForTest($1)($2)($3)"""
        |> ignore
    let! passResult =
        executeTool tool "call-1" (createObj [ "verdict", box "PASS" ]) ctx
    let! firstResolved =
        emitJsExpr firstPending "$0.promise"
        |> unbox<JS.Promise<obj>>
    equal "setPending PASS feedback absent" true (Dyn.isNullish (Dyn.get firstResolved "feedback"))
    equal "setPending PASS tool text" "Review submitted: accepted." (toolText passResult)

    let secondPending =
        emitJsExpr () "Promise.withResolvers()"
        |> unbox<obj>
    emitJsExpr (_test, reviewSessionId, parentSessionId, secondPending)
        """$0.setPendingReviewStateForTest($1)($2)($3)"""
        |> ignore
    let! rejectResult =
        executeTool tool "call-2" (createObj [ "verdict", box "REJECT"; "feedback", box "Fix it" ]) ctx
    let! secondResolved =
        emitJsExpr secondPending "$0.promise"
        |> unbox<JS.Promise<obj>>
    equal "setPending REJECT feedback" "Fix it" (str secondResolved "feedback")
    equal "setPending reject tool text" "Review submitted: rejected with feedback." (toolText rejectResult)
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
            store.resolvePendingReview(childId, Accepted) |> ignore
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
            store.resolvePendingReview(childId, Accepted) |> ignore
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
