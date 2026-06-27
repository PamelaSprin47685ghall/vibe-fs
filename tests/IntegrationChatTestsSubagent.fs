module Wanxiangshu.Tests.IntegrationChatTestsSubagent

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Opencode.Plugin
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Opencode.SessionIo
open Wanxiangshu.Shell.Dyn

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
    let! result = runSubagent (Wanxiangshu.Shell.FallbackRuntimeState.FallbackRuntimeState()) registry mockClient "browser" "Browser" "navigate to example.com" workspaceDir "parent-session-456" (createObj [ "abort", box null ]) null
    check "runSubagent returns Ok" (result.IsOk)
    check "runSubagent text" (match result with Ok t -> t.Contains "Example Domain" | _ -> false)
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
    do! runSubagent (Wanxiangshu.Shell.FallbackRuntimeState.FallbackRuntimeState()) registry (mkClient ()) "browser" "Browser" "first" workspaceDir "root-session" (createObj [ "abort", box null ]) null |> Promise.map (fun _ -> ())
    do! runSubagent (Wanxiangshu.Shell.FallbackRuntimeState.FallbackRuntimeState()) registry (mkClient ()) "coder" "Coder" "second" workspaceDir "child-1" (createObj [ "abort", box null ]) null |> Promise.map (fun _ -> ())
    check "nested subagent resolves to root parent" (str (get createCalls.[1] "body") "parentID" = "root-session")
    do! rmAsync workspaceDir
}

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
        runSubagent (Wanxiangshu.Shell.FallbackRuntimeState.FallbackRuntimeState()) registry mockClient "investigator" "Investigator" "trace the fault" workspaceDir "parent-aborted-session" parentContext null
    check "already-aborted parent yields (aborted)" (match result with Ok t -> t = "(aborted)" | _ -> false)
    check "child session.prompt not called when parent aborted" (promptCalls.Count = 0)
    check "host session.abort called once for child" (abortCalls.Count = 1)
    check "session.abort path targets child session" (str (get abortCalls.[0] "path") "id" = childSessionId)
    check "child session not left in ChildAgentRegistry" (registry.LookupChildAgent childSessionId |> Option.isNone)
    do! rmAsync workspaceDir
}

let run () : JS.Promise<unit> =
    promise {
        do! websearchBoundariesSpec ()
        do! subagentParentSpec ()
        do! nestedSubagentSpec ()
        do! subagentParentAlreadyAbortedSpec ()
    }