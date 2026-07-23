module Wanxiangshu.Tests.IntegrationChatTestsSubagent

open Wanxiangshu.Runtime.Fallback.RuntimeStore

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Hosts.Opencode.Plugin
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Hosts.Opencode.SessionIo
open Wanxiangshu.Runtime.Dyn


let subagentParentAlreadyAbortedSpec () =
    promise {
        let promptCalls = ResizeArray<obj>()
        let abortCalls = ResizeArray<obj>()
        let childSessionId = "child-abort-cascade-1"

        let mockClient =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "create",
                            box (
                                System.Func<obj, JS.Promise<obj>>(fun _ ->
                                    (promise { return box {| data = box {| id = childSessionId |} |} }))
                            )
                            "prompt",
                            box (System.Func<obj, JS.Promise<unit>>(fun arg -> (promise { promptCalls.Add(arg) })))
                            "messages",
                            box (
                                System.Func<obj, JS.Promise<obj>>(fun _ ->
                                    (promise {
                                        return
                                            box
                                                {| data =
                                                    [| box
                                                           {| info = box {| role = "assistant" |}
                                                              parts =
                                                               [| box
                                                                      {| ``type`` = "text"
                                                                         text = "should not be returned" |} |] |} |] |}
                                    }))
                            )
                            "abort",
                            box (System.Func<obj, JS.Promise<unit>>(fun arg -> (promise { abortCalls.Add(arg) })))
                            "delete", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ())) ]
                  ) ]

        let registry = ChildAgentRegistry.Create()
        let! workspaceDir = mkdtempAsync "subagent-parent-abort-"
        let abortedParentSignal = createObj [ "aborted", box true ]
        let parentContext = createObj [ "abort", box abortedParentSignal ]

        let! result =
            runSubagent
                (FallbackRuntimeStore())
                registry
                mockClient
                "inspector"
                "Inspector"
                "trace the fault"
                workspaceDir
                "parent-aborted-session"
                parentContext
                null

        check
            "already-aborted parent yields (aborted)"
            (match result with
             | Ok t -> t = "(aborted)"
             | _ -> false)

        check "child session.prompt not called when parent aborted" (promptCalls.Count = 0)
        check "host session.abort called once for child" (abortCalls.Count = 1)
        check "session.abort path targets child session" (str (get abortCalls.[0] "path") "id" = childSessionId)
        check "child session not left in ChildAgentRegistry" (registry.LookupChildAgent childSessionId |> Option.isNone)
        do! rmAsync workspaceDir
    }

let runAll () : JS.Promise<unit> =
    promise { do! subagentParentAlreadyAbortedSpec () }
