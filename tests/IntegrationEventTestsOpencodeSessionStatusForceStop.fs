module Wanxiangshu.Tests.IntegrationEventTestsOpencodeSessionStatusForceStop

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Tests.TempWorkspace
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

let opencodeForceStopTodoNudgeSpec () =
    promise {
        let sessionID = "force-stop-ws"

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
                                 text = "working on it" |} |] |} |]

        let promptCalls = ResizeArray<obj>()

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
                            box (System.Func<obj, JS.Promise<unit>>(fun arg -> promise { promptCalls.Add(arg) })) ]
                  ) ]

        let! workspaceDir = mkdtempAsync "force-stop-"

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = mkClient () |}
            )

        let eventHook = get p "event"

        let mkEvent typ props =
            box {| event = box {| ``type`` = typ; properties = props |} |}

        do!
            eventHook $ (mkEvent "stream-abort" (box {| sessionID = sessionID |}))
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()

        do!
            eventHook $ (mkEvent "session.idle" (box {| sessionID = sessionID |}))
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()
        check "force-stop must not send todo nudge" (promptCalls.Count = 0)
        do! rmAsync workspaceDir
    }

let sessionStatusIdleDoesNotTriggerNudgeSpec () =
    promise {
        let messages =
            [| box
                   {| info =
                       box
                           {| role = "assistant"
                              agent = "manager"
                              finish = "tool_calls"
                              time = box {| completed = 1 |} |}
                      parts =
                       [| box
                              {| ``type`` = "text"
                                 text = "still working" |} |] |} |]

        let promptCalls = ResizeArray<obj>()

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
                            box (System.Func<obj, JS.Promise<unit>>(fun arg -> promise { promptCalls.Add(arg) })) ]
                  ) ]

        let! workspaceDir = mkdtempAsync "session-status-idle-no-nudge-"

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = mkClient () |}
            )

        let eventHook = get p "event"
        let sessionID = "status-idle-session"

        let statusIdle =
            box
                {| event =
                    box
                        {| ``type`` = "session.status"
                           properties =
                            box
                                {| sessionID = sessionID
                                   status = box {| ``type`` = "idle" |} |} |} |}

        do! eventHook $ statusIdle |> unbox<JS.Promise<unit>>
        do! yieldMicrotask ()
        check "session.status idle with tool_calls finish does not trigger nudge" (promptCalls.Count = 0)
        do! rmAsync workspaceDir
    }
