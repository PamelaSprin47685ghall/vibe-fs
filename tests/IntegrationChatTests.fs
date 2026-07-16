module Wanxiangshu.Tests.IntegrationChatTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace

open Wanxiangshu.Hosts.Opencode.Plugin
open Wanxiangshu.Runtime.Dyn

let chatMessageSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "chat-message-"
        let! p = plugin (box {| directory = workspaceDir |})
        let chatMsg = get p "chat.message"

        let orchChat =
            createObj
                [ "message",
                  box (
                      createObj
                          [ "tools",
                            box (
                                createObj
                                    [ "stealth-browser-mcp_*", box true
                                      "stealth-browser-mcp_foo", box true
                                      "write", box true
                                      "read", box true ]
                            ) ]
                  ) ]

        do!
            chatMsg
            $ (box
                {| sessionID = "root"
                   agent = "manager" |},
               orchChat)
            |> unbox<JS.Promise<unit>>

        let tools = get (get orchChat "message") "tools"
        check "manager stealth disabled" (not (unbox<bool> (get tools "stealth-browser-mcp_*")))
        check "manager stealth foo disabled" (not (unbox<bool> (get tools "stealth-browser-mcp_foo")))
        check "manager write disabled" (not (unbox<bool> (get tools "write")))
        check "manager read preserved" (unbox<bool> (get tools "read"))

        let coderChat =
            createObj
                [ "message",
                  box (
                      createObj
                          [ "tools",
                            box (
                                createObj
                                    [ "stealth-browser-mcp_bar", box true
                                      "stealth-browser-mcp_*", box true
                                      "patch", box true ]
                            ) ]
                  ) ]

        do!
            chatMsg
            $ (box
                {| sessionID = "root"
                   agent = "coder" |},
               coderChat)
            |> unbox<JS.Promise<unit>>

        let coderTools = get (get coderChat "message") "tools"
        check "coder stealth bar disabled" (not (unbox<bool> (get coderTools "stealth-browser-mcp_bar")))
        check "coder stealth star disabled" (not (unbox<bool> (get coderTools "stealth-browser-mcp_*")))
        check "coder patch preserved" (unbox<bool> (get coderTools "patch"))

        let browserChat =
            createObj
                [ "message",
                  box (
                      createObj
                          [ "tools",
                            box (
                                createObj
                                    [ "stealth-browser-mcp_*", box true
                                      "stealth-browser-mcp_foo", box true
                                      "read", box true ]
                            ) ]
                  ) ]

        do!
            chatMsg
            $ (box
                {| sessionID = "root"
                   agent = "browser" |},
               browserChat)
            |> unbox<JS.Promise<unit>>

        let browserTools = get (get browserChat "message") "tools"
        check "browser stealth star preserved" (unbox<bool> (get browserTools "stealth-browser-mcp_*"))
        check "browser stealth foo preserved" (unbox<bool> (get browserTools "stealth-browser-mcp_foo"))
        check "browser read preserved" (unbox<bool> (get browserTools "read"))
        do! rmAsync workspaceDir
    }

let childAgentChatSpec () =
    promise {
        let mkObjPromise f =
            box (System.Func<unit, JS.Promise<obj>>(fun () -> f ()))

        let mkUnitPromise f =
            box (System.Func<unit, JS.Promise<unit>>(fun () -> f ()))

        let createSession =
            (promise { return box {| data = box {| id = "child-browser-session" |} |} })

        let doPrompt = Promise.lift ()
        let getMessages = (promise { return box {| data = [||] |} })

        let clientObj =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "create", mkObjPromise (fun () -> createSession)
                            "prompt", mkUnitPromise (fun () -> doPrompt)
                            "messages", mkObjPromise (fun () -> getMessages) ]
                  ) ]

        let! workspaceDir = mkdtempAsync "child-agent-chat-"

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = clientObj |}
            )

        let chatMsg = get p "chat.message"

        let childChat =
            createObj [ "message", box (createObj [ "tools", box (createObj [ "stealth-browser-mcp_*", box true ]) ]) ]

        do!
            chatMsg
            $ (box
                {| sessionID = "child-browser-session"
                   agent = "browser" |},
               childChat)
            |> unbox<JS.Promise<unit>>

        let tools = get (get childChat "message") "tools"
        check "child session resolves to browser" (unbox<bool> (get tools "stealth-browser-mcp_*"))

        let childOrchChat =
            createObj [ "message", box (createObj [ "tools", box (createObj [ "stealth-browser-mcp_*", box true ]) ]) ]

        do!
            chatMsg
            $ (box
                {| sessionID = "child-orch-session"
                   agent = "manager" |},
               childOrchChat)
            |> unbox<JS.Promise<unit>>

        let orchTools = get (get childOrchChat "message") "tools"
        check "child session resolves to orchestrator" (not (unbox<bool> (get orchTools "stealth-browser-mcp_*")))
        do! rmAsync workspaceDir
    }

let childExecutorChatWithoutInputAgentSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "child-executor-chat-"
        let! p = plugin (box {| directory = workspaceDir |})
        let chatMsg = get p "chat.message"

        let chat =
            createObj
                [ "message",
                  box (
                      createObj
                          [ "info",
                            box (createObj [ "agent", box "executor"; "sessionID", box "child-executor-session" ])
                            "tools", box (createObj [ "read", box true ]) ]
                  ) ]

        do! chatMsg $ (createObj [], chat) |> unbox<JS.Promise<unit>>
        let tools = get (get chat "message") "tools"

        check
            "executor child chat hides read (executor in defaultExcludedAgents)"
            (not (unbox<bool> (get tools "read")))

        do! rmAsync workspaceDir
    }

let run () : JS.Promise<unit> =
    promise {
        do! chatMessageSpec ()
        do! childAgentChatSpec ()
        do! childExecutorChatWithoutInputAgentSpec ()
    }
