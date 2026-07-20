module Wanxiangshu.Tests.IntegrationEventTestsOpencodeSessionStatus

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Hosts.Opencode.Plugin
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Messaging.OpencodeSessionEventCodec

let private promptText (arg: obj) =
    getPartsText (get (get arg "body") "parts")

let private userTextMessage sessionID text =
    box (
        createObj
            [ "info", box (createObj [ "role", box "user"; "sessionID", box sessionID ])
              "parts", box [| createObj [ "type", box "text"; "text", box text ] |] ]
    )

let sessionStatusIdleAndSessionIdleDedupSpec () =
    promise {
        let sessionID = "dedup-session"

        let mutable messages =
            [| box
                   {| info =
                       box
                           {| role = "assistant"
                              agent = "manager"
                              finish = "stop"
                              time = box {| completed = 1 |} |}
                      parts =
                       [| box
                              {| ``type`` = "text"
                                 text = "still working" |} |] |} |]

        let promptCalls = ResizeArray<obj>()
        let! workspaceDir = mkdtempAsync "session-status-idle-dedup-"

        let mkClient () =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "todo",
                            box (
                                System.Func<unit, JS.Promise<obj>>(fun () ->
                                    promise {
                                        return
                                            box
                                                {| data =
                                                    [| box
                                                           {| id = "todo-1"
                                                              content = "task"
                                                              status = "in_progress" |} |] |}
                                    })
                            )
                            "messages",
                            box (
                                System.Func<unit, JS.Promise<obj>>(fun () ->
                                    promise { return box {| data = messages |} })
                            )
                            "prompt",
                            box (
                                System.Func<obj, JS.Promise<unit>>(fun arg ->
                                    promise {
                                        resolveNudgeReceiptFromPromptArg workspaceDir arg
                                        promptCalls.Add(arg)

                                        messages <-
                                            Array.append messages [| userTextMessage sessionID (promptText arg) |]
                                    })
                            ) ]
                  ) ]

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = mkClient () |}
            )

        let eventHook = get p "event"

        let statusIdle =
            box
                {| event =
                    box
                        {| ``type`` = "session.status"
                           properties =
                            box
                                {| sessionID = sessionID
                                   status = box {| ``type`` = "idle" |} |} |} |}

        let idle =
            box
                {| event =
                    box
                        {| ``type`` = "session.idle"
                           properties = box {| sessionID = sessionID |} |} |}

        do! eventHook $ statusIdle |> unbox<JS.Promise<unit>>
        do! yieldMicrotask ()
        do! eventHook $ idle |> unbox<JS.Promise<unit>>
        do! yieldMicrotask ()
        check "session.status idle + session.idle for same session sends nudge once" (promptCalls.Count = 1)
        do! rmAsync workspaceDir
    }

let sessionStatusBusyDoesNotNudgeSpec () =
    promise {
        let messages =
            [| box
                   {| info =
                       box
                           {| role = "assistant"
                              agent = "manager"
                              finish = "stop"
                              time = box {| completed = 1 |} |}
                      parts =
                       [| box
                              {| ``type`` = "text"
                                 text = "still working" |} |] |} |]

        let promptCalls = ResizeArray<obj>()
        let! workspaceDir = mkdtempAsync "session-status-busy-"

        let mkClient () =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "todo",
                            box (
                                System.Func<unit, JS.Promise<obj>>(fun () ->
                                    promise {
                                        return
                                            box
                                                {| data =
                                                    [| box
                                                           {| id = "todo-1"
                                                              content = "task"
                                                              status = "in_progress" |} |] |}
                                    })
                            )
                            "messages",
                            box (
                                System.Func<unit, JS.Promise<obj>>(fun () ->
                                    promise { return box {| data = messages |} })
                            )
                            "prompt",
                            box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                                promise {
                                    resolveNudgeReceiptFromPromptArg workspaceDir arg
                                    promptCalls.Add(arg)
                                })) ]
                  ) ]

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = mkClient () |}
            )

        let eventHook = get p "event"
        let sessionID = "busy-session"

        let statusBusy =
            box
                {| event =
                    box
                        {| ``type`` = "session.status"
                           properties =
                            box
                                {| sessionID = sessionID
                                   status = box {| ``type`` = "busy" |} |} |} |}

        do! eventHook $ statusBusy |> unbox<JS.Promise<unit>>
        do! yieldMicrotask ()
        check "session.status busy does not nudge while agent is working" (promptCalls.Count = 0)
        do! rmAsync workspaceDir
    }

let reusedSessionSpec () =
    promise {
        let mutable messages =
            [| box
                   {| info =
                       box
                           {| role = "assistant"
                              agent = "manager"
                              finish = "stop"
                              time = box {| completed = 1 |} |}
                      parts =
                       [| box
                              {| ``type`` = "text"
                                 text = "reopened work" |} |] |} |]

        let promptCalls = ResizeArray<obj>()
        let! workspaceDir = mkdtempAsync "reused-session-"

        let mkClient () =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "todo",
                            box (
                                System.Func<unit, JS.Promise<obj>>(fun () ->
                                    (promise {
                                        return
                                            box
                                                {| data =
                                                    [| box
                                                           {| id = "todo-1"
                                                              content = "task"
                                                              status = "in_progress" |} |] |}
                                    }))
                            )
                            "messages",
                            box (
                                System.Func<unit, JS.Promise<obj>>(fun () ->
                                    (promise { return box {| data = messages |} }))
                            )
                            "prompt",
                            box (System.Func<obj, JS.Promise<unit>>(fun arg ->
                                (promise {
                                    resolveNudgeReceiptFromPromptArg workspaceDir arg
                                    promptCalls.Add(arg)
                                }))) ]
                  ) ]

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = mkClient () |}
            )

        let eventHook = get p "event"

        do!
            eventHook
            $ (box
                {| event =
                    box
                        {| ``type`` = "session.idle"
                           properties = box {| sessionID = "reused-session" |} |} |})
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()

        do!
            eventHook
            $ (box
                {| event =
                    box
                        {| ``type`` = "session.deleted"
                           properties = box {| info = box {| id = "reused-session" |} |} |} |})
            |> unbox<JS.Promise<unit>>

        messages <-
            [| box
                   {| info =
                       box
                           {| role = "assistant"
                              agent = "manager"
                              finish = "stop"
                              time = box {| completed = 1 |} |}
                      parts =
                       [| box
                              {| ``type`` = "text"
                                 text = "reopened work" |} |] |} |]

        let! p2 =
            plugin (
                box
                    {| directory = workspaceDir
                       client = mkClient () |}
            )

        do!
            (get p2 "event")
            $ (box
                {| event =
                    box
                        {| ``type`` = "session.idle"
                           properties = box {| sessionID = "reused-session" |} |} |})
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()
        check "reused session nudges after session.deleted" (promptCalls.Count = 2)
        do! rmAsync workspaceDir
    }
