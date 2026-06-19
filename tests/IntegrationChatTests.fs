module VibeFs.Tests.IntegrationChatTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Kernel.Dyn
open VibeFs.Opencode.Plugin
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Opencode.SessionIo

let chatMessageSpec () = async {
    let! workspaceDir = mkdtempAsync "chat-message-" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let chatMsg = get p "chat.message"
    let orchChat = createObj [ "message", box (createObj [ "tools", box (createObj [
        "stealth-browser-mcp_*", box true
        "stealth-browser-mcp_foo", box true
        "write", box true
        "read", box true
    ]) ]) ]
    do! chatMsg $ (box {| sessionID = "root"; agent = "manager" |}, orchChat) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
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
    do! chatMsg $ (box {| sessionID = "root"; agent = "coder" |}, coderChat) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    let coderTools = get (get coderChat "message") "tools"
    check "coder stealth bar disabled" (not (unbox<bool> (get coderTools "stealth-browser-mcp_bar")))
    check "coder stealth star disabled" (not (unbox<bool> (get coderTools "stealth-browser-mcp_*")))
    check "coder patch preserved" (unbox<bool> (get coderTools "patch"))
    let browserChat = createObj [ "message", box (createObj [ "tools", box (createObj [
        "stealth-browser-mcp_*", box true
        "stealth-browser-mcp_foo", box true
        "read", box true
    ]) ]) ]
    do! chatMsg $ (box {| sessionID = "root"; agent = "browser" |}, browserChat) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    let browserTools = get (get browserChat "message") "tools"
    check "browser stealth star preserved" (unbox<bool> (get browserTools "stealth-browser-mcp_*"))
    check "browser stealth foo preserved" (unbox<bool> (get browserTools "stealth-browser-mcp_foo"))
    check "browser read preserved" (unbox<bool> (get browserTools "read"))
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let childAgentChatSpec () = async {
    let mkObjPromise f = box (System.Func<unit, JS.Promise<obj>>(fun () -> f ()))
    let mkUnitPromise f = box (System.Func<unit, JS.Promise<unit>>(fun () -> f ()))
    let createSession = (async { return box {| data = box {| id = "child-browser-session" |} |} } |> Async.StartAsPromise)
    let doPrompt = (async { () } |> Async.StartAsPromise)
    let getMessages = (async { return box {| data = [||] |} } |> Async.StartAsPromise)
    let clientObj =
        createObj [ "session", box (createObj [
            "create", mkObjPromise (fun () -> createSession)
            "prompt", mkUnitPromise (fun () -> doPrompt)
            "messages", mkObjPromise (fun () -> getMessages)
        ]) ]
    let! workspaceDir = mkdtempAsync "child-agent-chat-" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir; client = clientObj |}) |> Async.AwaitPromise
    let chatMsg = get p "chat.message"
    let childChat = createObj [ "message", box (createObj [ "tools", box (createObj [
        "stealth-browser-mcp_*", box true
    ]) ]) ]
    do! chatMsg $ (box {| sessionID = "child-browser-session"; agent = "browser" |}, childChat) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    let tools = get (get childChat "message") "tools"
    check "child session resolves to browser" (unbox<bool> (get tools "stealth-browser-mcp_*"))
    let childOrchChat = createObj [ "message", box (createObj [ "tools", box (createObj [
        "stealth-browser-mcp_*", box true
    ]) ]) ]
    do! chatMsg $ (box {| sessionID = "child-orch-session"; agent = "manager" |}, childOrchChat) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    let orchTools = get (get childOrchChat "message") "tools"
    check "child session resolves to orchestrator" (not (unbox<bool> (get orchTools "stealth-browser-mcp_*")))
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let websearchBoundariesSpec () = async {
    let! workspaceDir = mkdtempAsync "websearch-boundaries-" |> Async.AwaitPromise
    let! p = plugin (box {| directory = workspaceDir |}) |> Async.AwaitPromise
    let chatMsg = get p "chat.message"
    let coderChat = createObj [ "message", box (createObj [ "tools", box (createObj [
        "websearch", box true
        "webfetch", box true
    ]) ]) ]
    do! chatMsg $ (box {| sessionID = "root"; agent = "coder" |}, coderChat) |> unbox<JS.Promise<unit>> |> Async.AwaitPromise
    let tools = get (get coderChat "message") "tools"
    check "coder websearch forced false" (not (unbox<bool> (get tools "websearch")))
    check "coder webfetch forced false" (not (unbox<bool> (get tools "webfetch")))
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let subagentParentSpec () = async {
    let createCalls = ResizeArray<obj>()
    let promptCalls = ResizeArray<obj>()
    let mockClient =
        createObj [ "session", box (createObj [
            "create", box (System.Func<obj, JS.Promise<obj>>(fun arg ->
                (async { createCalls.Add(arg); return box {| data = box {| id = "child-session-123" |} |} } |> Async.StartAsPromise)))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                (async { promptCalls.Add(arg) } |> Async.StartAsPromise)))
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (async { return box {| data = [|
                    box {| info = box {| role = "user" |}; parts = [| box {| ``type`` = "text"; text = "navigate to example.com" |} |] |}
                    box {| info = box {| role = "assistant" |}; parts = [| box {| ``type`` = "text"; text = "Found the page title: Example Domain" |} |] |}
                |] |} } |> Async.StartAsPromise)))
            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ ->
                (async { () } |> Async.StartAsPromise)))
        ]) ]
    let registry = ChildAgentRegistry.Create()
    let! workspaceDir = mkdtempAsync "subagent-parent-" |> Async.AwaitPromise
    let! result = runSubagent registry mockClient "browser" "Browser" "navigate to example.com" workspaceDir "parent-session-456" (createObj [ "abort", box null ]) null |> Async.AwaitPromise
    check "runSubagent returns string" (result.Contains "Example Domain")
    check "session.create received parentID" (str (get createCalls.[0] "body") "parentID" = "parent-session-456")
    check "session.prompt uses child id" (str (get promptCalls.[0] "path") "id" = "child-session-123")
    check "session.prompt uses browser agent" (str (get promptCalls.[0] "body") "agent" = "browser")
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let nestedSubagentSpec () = async {
    let createCalls = ResizeArray<obj>()
    let mkClient () =
        createObj [ "session", box (createObj [
            "create", box (System.Func<obj, JS.Promise<obj>>(fun arg ->
                (async { createCalls.Add(arg); return box {| data = box {| id = $"child-{createCalls.Count}" |} |} } |> Async.StartAsPromise)))
            "prompt", box (System.Func<obj, JS.Promise<unit>>(fun _ ->
                (async { () } |> Async.StartAsPromise)))
            "messages", box (System.Func<obj, JS.Promise<obj>>(fun _ ->
                (async { return box {| data = [||] |} } |> Async.StartAsPromise)))
            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ ->
                (async { () } |> Async.StartAsPromise)))
        ]) ]
    let registry = ChildAgentRegistry.Create()
    let! workspaceDir = mkdtempAsync "nested-subagent-" |> Async.AwaitPromise
    do! runSubagent registry (mkClient ()) "browser" "Browser" "first" workspaceDir "root-session" (createObj [ "abort", box null ]) null |> Async.AwaitPromise |> Async.Ignore
    do! runSubagent registry (mkClient ()) "coder" "Coder" "second" workspaceDir "child-1" (createObj [ "abort", box null ]) null |> Async.AwaitPromise |> Async.Ignore
    check "nested subagent resolves to root parent" (str (get createCalls.[1] "body") "parentID" = "root-session")
    do! rmAsync workspaceDir |> Async.AwaitPromise
}

let run () : JS.Promise<unit> =
    async {
        do! chatMessageSpec ()
        do! childAgentChatSpec ()
        do! websearchBoundariesSpec ()
        do! subagentParentSpec ()
        do! nestedSubagentSpec ()
    }
    |> Async.StartAsPromise
