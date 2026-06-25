module VibeFs.Tests.IntegrationChatTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace

open VibeFs.Opencode.Plugin
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Opencode.SessionIo
open VibeFs.Shell.Dyn

let chatMessageSpec () = promise {
    let! workspaceDir = mkdtempAsync "chat-message-"
    let! p = plugin (box {| directory = workspaceDir |})
    let chatMsg = get p "chat.message"
    let orchChat = createObj [ "message", box (createObj [ "tools", box (createObj [
        "stealth-browser-mcp_*", box true
        "stealth-browser-mcp_foo", box true
        "write", box true
        "read", box true
    ]) ]) ]
    do! chatMsg $ (box {| sessionID = "root"; agent = "manager" |}, orchChat) |> unbox<JS.Promise<unit>>
    let tools = get (get orchChat "message") "tools"
    check "manager stealth disabled" (not (unbox<bool> (get tools "stealth-browser-mcp_*")))
    check "manager stealth foo disabled" (not (unbox<bool> (get tools "stealth-browser-mcp_foo")))
    check "manager write disabled" (not (unbox<bool> (get tools "write")))
    check "manager read preserved" (unbox<bool> (get tools "read"))
    let coderChat = createObj [ "message", box (createObj [ "tools", box (createObj [
        "stealth-browser-mcp_bar", box true
        "stealth-browser-mcp_*", box true
        "patch", box true
    ]) ]) ]
    do! chatMsg $ (box {| sessionID = "root"; agent = "coder" |}, coderChat) |> unbox<JS.Promise<unit>>
    let coderTools = get (get coderChat "message") "tools"
    check "coder stealth bar disabled" (not (unbox<bool> (get coderTools "stealth-browser-mcp_bar")))
    check "coder stealth star disabled" (not (unbox<bool> (get coderTools "stealth-browser-mcp_*")))
    check "coder patch preserved" (unbox<bool> (get coderTools "patch"))
    let browserChat = createObj [ "message", box (createObj [ "tools", box (createObj [
        "stealth-browser-mcp_*", box true
        "stealth-browser-mcp_foo", box true
        "read", box true
    ]) ]) ]
    do! chatMsg $ (box {| sessionID = "root"; agent = "browser" |}, browserChat) |> unbox<JS.Promise<unit>>
    let browserTools = get (get browserChat "message") "tools"
    check "browser stealth star preserved" (unbox<bool> (get browserTools "stealth-browser-mcp_*"))
    check "browser stealth foo preserved" (unbox<bool> (get browserTools "stealth-browser-mcp_foo"))
    check "browser read preserved" (unbox<bool> (get browserTools "read"))
    do! rmAsync workspaceDir
}

let childAgentChatSpec () = promise {
    let mkObjPromise f = box (System.Func<unit, JS.Promise<obj>>(fun () -> f ()))
    let mkUnitPromise f = box (System.Func<unit, JS.Promise<unit>>(fun () -> f ()))
    let createSession = (promise { return box {| data = box {| id = "child-browser-session" |} |} })
    let doPrompt = Promise.lift ()
    let getMessages = (promise { return box {| data = [||] |} })
    let clientObj =
        createObj [ "session", box (createObj [
            "create", mkObjPromise (fun () -> createSession)
            "prompt", mkUnitPromise (fun () -> doPrompt)
            "messages", mkObjPromise (fun () -> getMessages)
        ]) ]
    let! workspaceDir = mkdtempAsync "child-agent-chat-"
    let! p = plugin (box {| directory = workspaceDir; client = clientObj |})
    let chatMsg = get p "chat.message"
    let childChat = createObj [ "message", box (createObj [ "tools", box (createObj [
        "stealth-browser-mcp_*", box true
    ]) ]) ]
    do! chatMsg $ (box {| sessionID = "child-browser-session"; agent = "browser" |}, childChat) |> unbox<JS.Promise<unit>>
    let tools = get (get childChat "message") "tools"
    check "child session resolves to browser" (unbox<bool> (get tools "stealth-browser-mcp_*"))
    let childOrchChat = createObj [ "message", box (createObj [ "tools", box (createObj [
        "stealth-browser-mcp_*", box true
    ]) ]) ]
    do! chatMsg $ (box {| sessionID = "child-orch-session"; agent = "manager" |}, childOrchChat) |> unbox<JS.Promise<unit>>
    let orchTools = get (get childOrchChat "message") "tools"
    check "child session resolves to orchestrator" (not (unbox<bool> (get orchTools "stealth-browser-mcp_*")))
    do! rmAsync workspaceDir
}

let childExecutorChatWithoutInputAgentSpec () = promise {
    let! workspaceDir = mkdtempAsync "child-executor-chat-"
    let! p = plugin (box {| directory = workspaceDir |})
    let chatMsg = get p "chat.message"
    let chat = createObj [ "message", box (createObj [
        "info", box (createObj [ "agent", box "executor"; "sessionID", box "child-executor-session" ])
        "tools", box (createObj [
            "knowledge_graph_fetch", box true
            "read", box true
        ])
    ]) ]
    do! chatMsg $ (createObj [], chat) |> unbox<JS.Promise<unit>>
    let tools = get (get chat "message") "tools"
    check "executor child chat hides knowledge_graph_fetch without input agent" (not (unbox<bool> (get tools "knowledge_graph_fetch")))
    check "executor child chat also hides read" (not (unbox<bool> (get tools "read")))
    do! rmAsync workspaceDir
}

let websearchBoundariesSpec () = promise {
    let! workspaceDir = mkdtempAsync "websearch-boundaries-"
    let! p = plugin (box {| directory = workspaceDir |})
    let chatMsg = get p "chat.message"
    let coderChat = createObj [ "message", box (createObj [ "tools", box (createObj [
        "websearch", box true
        "webfetch", box true
    ]) ]) ]
    do! chatMsg $ (box {| sessionID = "root"; agent = "coder" |}, coderChat) |> unbox<JS.Promise<unit>>
    let tools = get (get coderChat "message") "tools"
    check "coder websearch forced false" (not (unbox<bool> (get tools "websearch")))
    check "coder webfetch forced false" (not (unbox<bool> (get tools "webfetch")))
    do! rmAsync workspaceDir
}

let subagentParentSpec () = promise {
    let createCalls = ResizeArray<obj>()
    let promptCalls = ResizeArray<obj>()
    let mockClient =
        createObj [ "session", box (createObj [
            "create", box (System.Func<obj, JS.Promise<obj>>(fun arg ->
                (promise { createCalls.Add(arg); return box {| data = box {| id = "child-session-123" |} |} })))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                (promise { promptCalls.Add(arg) })))
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (promise { return box {| data = [|
                    box {| info = box {| role = "user" |}; parts = [| box {| ``type`` = "text"; text = "navigate to example.com" |} |] |}
                    box {| info = box {| role = "assistant" |}; parts = [| box {| ``type`` = "text"; text = "Found the page title: Example Domain" |} |] |}
                |] |} })))
            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ ->
                Promise.lift ()))
        ]) ]
    let registry = ChildAgentRegistry.Create()
    let! workspaceDir = mkdtempAsync "subagent-parent-"
    let! result = runSubagent registry mockClient "browser" "Browser" "navigate to example.com" workspaceDir "parent-session-456" (createObj [ "abort", box null ]) null
    check "runSubagent returns string" (result.Contains "Example Domain")
    check "session.create received parentID" (str (get createCalls.[0] "body") "parentID" = "parent-session-456")
    check "session.prompt uses child id" (str (get promptCalls.[0] "path") "id" = "child-session-123")
    check "session.prompt uses browser agent" (str (get promptCalls.[0] "body") "agent" = "browser")
    do! rmAsync workspaceDir
}

let nestedSubagentSpec () = promise {
    let createCalls = ResizeArray<obj>()
    let mkClient () =
        createObj [ "session", box (createObj [
            "create", box (System.Func<obj, JS.Promise<obj>>(fun arg ->
                (promise { createCalls.Add(arg); return box {| data = box {| id = $"child-{createCalls.Count}" |} |} })))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun _ ->
                Promise.lift ()))
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (promise { return box {| data = [||] |} })))
            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ ->
                Promise.lift ()))
        ]) ]
    let registry = ChildAgentRegistry.Create()
    let! workspaceDir = mkdtempAsync "nested-subagent-"
    do! runSubagent registry (mkClient ()) "browser" "Browser" "first" workspaceDir "root-session" (createObj [ "abort", box null ]) null |> Promise.map ignore
    do! runSubagent registry (mkClient ()) "coder" "Coder" "second" workspaceDir "child-1" (createObj [ "abort", box null ]) null |> Promise.map ignore
    check "nested subagent resolves to root parent" (str (get createCalls.[1] "body") "parentID" = "root-session")
    do! rmAsync workspaceDir
}

/// Regression: parent tool context already aborted must cascade to child session
/// (no child prompt, host session.abort, registry cleanup, "(aborted)" result).
let subagentParentAlreadyAbortedSpec () = promise {
    let promptCalls = ResizeArray<obj>()
    let abortCalls = ResizeArray<obj>()
    let childSessionId = "child-abort-cascade-1"
    let mockClient =
        createObj [ "session", box (createObj [
            "create", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (promise { return box {| data = box {| id = childSessionId |} |} })))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                (promise { promptCalls.Add(arg) })))
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (promise {
                    return box {| data = [|
                        box {| info = box {| role = "assistant" |}; parts = [| box {| ``type`` = "text"; text = "should not be returned" |} |] |}
                    |] |}
                })))
            "abort", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                (promise { abortCalls.Add(arg) })))
        ]) ]
    let registry = ChildAgentRegistry.Create()
    let! workspaceDir = mkdtempAsync "subagent-parent-abort-"
    let abortedParentSignal = createObj [ "aborted", box true ]
    let parentContext = createObj [ "abort", box abortedParentSignal ]
    let! result =
        runSubagent registry mockClient "investigator" "Investigator" "trace the fault" workspaceDir "parent-aborted-session" parentContext null
    check "already-aborted parent yields (aborted)" (result = "(aborted)")
    check "child session.prompt not called when parent aborted" (promptCalls.Count = 0)
    check "host session.abort called once for child" (abortCalls.Count = 1)
    check "session.abort path targets child session" (str (get abortCalls.[0] "path") "id" = childSessionId)
    check "child session not left in ChildAgentRegistry" (registry.LookupChildAgent childSessionId |> Option.isNone)
    do! rmAsync workspaceDir
}

let run () : JS.Promise<unit> =
    promise {
        do! chatMessageSpec ()
        do! childAgentChatSpec ()
        do! childExecutorChatWithoutInputAgentSpec ()
        do! websearchBoundariesSpec ()
        do! subagentParentSpec ()
        do! nestedSubagentSpec ()
        do! subagentParentAlreadyAbortedSpec ()
    }
