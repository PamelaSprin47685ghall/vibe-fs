module Wanxiangshu.Tests.OmpSessionCompactingTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.OmpPluginTestsHarness
open Wanxiangshu.Omp.Plugin
open Wanxiangshu.Omp.SessionCompacting
open Wanxiangshu.Shell.Dyn
module Dyn = Wanxiangshu.Shell.Dyn

/// Empty messages array returns empty object per Pi contract.
let sessionCompactingHandlerEmptyMessages () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    let event = createObj [ "sessionId", box "test-session"; "messages", box [||] ]
    let! result = sessionCompactingHandler pi event (createObj [ "cwd", box "/tmp" ])
    let keys = JS.Constructors.Object.keys(result)
    check "empty messages: returns empty object" (Seq.length keys = 0)
}

/// Null or missing messages field returns empty object.
let sessionCompactingHandlerNullMessages () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    let eventNull = createObj [ "sessionId", box "test-session"; "messages", box null ]
    let! resultNull = sessionCompactingHandler pi eventNull (createObj [ "cwd", box "/tmp" ])
    let keysNull = JS.Constructors.Object.keys(resultNull)
    check "null messages: returns empty object" (Seq.length keysNull = 0)
    let eventMissing = createObj [ "sessionId", box "test-session" ]
    let! resultMissing = sessionCompactingHandler pi eventMissing (createObj [ "cwd", box "/tmp" ])
    let keysMissing = JS.Constructors.Object.keys(resultMissing)
    check "missing messages: returns empty object" (Seq.length keysMissing = 0)
}

/// Non-empty messages trigger backlog projection and return context lines.
let sessionCompactingHandlerWithMessages () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    let userEntry =
        createObj [
            "id", box "msg-user-1"
            "info", box(createObj [ "role", box "user" ])
            "parts", box [| createObj [ "type", box "text"; "text", box "start work" ] |]
        ]
    let todoEntry =
        createObj [
            "id", box "msg-todo-1"
            "info", box(createObj [ "role", box "assistant" ])
            "parts",
                box [|
                    createObj [
                        "type", box "tool"
                        "tool", box "todowrite"
                        "callID", box "call-tw-1"
                        "state", box(createObj [
                            "status", box "completed"
                            "output", box "Todos updated."
                            "error", box ""
                            "input", box(createObj [
                                "ahaMoments", box "Completed task A"
                                "changesAndReasons", box ""
                                "gotchas", box ""
                                "lessonsAndConventions", box ""
                                "plan", box ""
                            ])
                        ])
                    ]
                |]
        ]
    let event =
        createObj [
            "sessionId", box "test-session-compact"
            "messages", box [| userEntry; todoEntry |]
        ]
    let! result = sessionCompactingHandler pi event (createObj [ "cwd", box "/tmp" ])
    let context = Dyn.get result "context"
    check "with messages: has context field" (not (Dyn.isNullish context))
    check "with messages: context is array" (Dyn.isArray context)
    if Dyn.isArray context then
        let arr = unbox<string array> context
        check "with messages: context non-empty" (arr.Length > 0)
        let joined = System.String.Join("\n", arr)
        check "with messages: backlog contains report text" (joined.Contains "Completed task A")
}

/// Backlog projection includes ahaMoments in compaction output.
let sessionCompactingPreservesBacklogReport () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    let userMsg =
        createObj [
            "id", box "u1"
            "info", box(createObj [ "role", box "user" ])
            "parts", box [| createObj [ "type", box "text"; "text", box "implement feature X" ] |]
        ]
    let todo1 =
        createObj [
            "id", box "t1"
            "info", box(createObj [ "role", box "assistant" ])
            "parts",
                box [|
                    createObj [
                        "type", box "tool"
                        "tool", box "todowrite"
                        "callID", box "call-1"
                        "state", box(createObj [
                            "status", box "completed"
                            "output", box "Todos updated."
                            "error", box ""
                            "input", box(createObj [
                                "ahaMoments", box "Added module A with tests"
                                "changesAndReasons", box ""
                                "gotchas", box ""
                                "lessonsAndConventions", box ""
                                "plan", box ""
                            ])
                        ])
                    ]
                |]
        ]
    let todo2 =
        createObj [
            "id", box "t2"
            "info", box(createObj [ "role", box "assistant" ])
            "parts",
                box [|
                    createObj [
                        "type", box "tool"
                        "tool", box "todowrite"
                        "callID", box "call-2"
                        "state", box(createObj [
                            "status", box "completed"
                            "output", box "Todos updated."
                            "error", box ""
                            "input", box(createObj [
                                "ahaMoments", box "Refactored module B for clarity"
                                "changesAndReasons", box ""
                                "gotchas", box ""
                                "lessonsAndConventions", box ""
                                "plan", box ""
                            ])
                        ])
                    ]
                |]
        ]
    let event =
        createObj [
            "sessionId", box "session-reports"
            "messages", box [| userMsg; todo1; todo2 |]
        ]
    let! result = sessionCompactingHandler pi event (createObj [ "cwd", box "/tmp" ])
    let context = Dyn.get result "context"
    check "report preservation: context exists" (not (Dyn.isNullish context))
    if Dyn.isArray context then
        let lines = unbox<string array> context
        let text = System.String.Join("\n", lines)
        check "report preservation: first report present" (text.Contains "Added module A with tests")
        check "report preservation: second report present" (text.Contains "Refactored module B for clarity")
}

/// Backlog projection strips synthetic messages before projection.
let sessionCompactingStripsSynthetic () = promise {
    resetPluginState ()
    let h = createPiHarness ()
    let pi = piObject h
    do! wanxiangshuExtension pi
    let userMsg =
        createObj [
            "id", box "user-original"
            "info", box(createObj [ "role", box "user" ])
            "parts", box [| createObj [ "type", box "text"; "text", box "original prompt" ] |]
        ]
    let syntheticMsg =
        createObj [
            "id", box "backlog-prefix-synthetic"
            "info", box(createObj [ "role", box "user" ])
            "parts", box [| createObj [ "type", box "text"; "text", box "--- previous context ---" ] |]
        ]
    let todoMsg =
        createObj [
            "id", box "todo-work"
            "info", box(createObj [ "role", box "assistant" ])
            "parts",
                box [|
                    createObj [
                        "type", box "tool"
                        "tool", box "todowrite"
                        "callID", box "call-tw-1"
                        "state", box(createObj [
                            "status", box "completed"
                            "output", box "Todos updated."
                            "error", box ""
                            "input", box(createObj [
                                "ahaMoments", box "Work done"
                                "changesAndReasons", box ""
                                "gotchas", box ""
                                "lessonsAndConventions", box ""
                                "plan", box ""
                            ])
                        ])
                    ]
                |]
        ]
    let event =
        createObj [
            "sessionId", box "session-synthetic"
            "messages", box [| userMsg; syntheticMsg; todoMsg |]
        ]
    let! result = sessionCompactingHandler pi event (createObj [ "cwd", box "/tmp" ])
    let context = Dyn.get result "context"
    if Dyn.isArray context then
        let lines = unbox<string array> context
        let text = System.String.Join("\n", lines)
        check "strips synthetic: synthetic prefix removed" (not (text.Contains "backlog-prefix-synthetic"))
        check "strips synthetic: actual work preserved" (text.Contains "Work done")
}
