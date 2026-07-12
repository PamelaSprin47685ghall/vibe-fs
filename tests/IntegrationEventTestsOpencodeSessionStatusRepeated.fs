module Wanxiangshu.Tests.IntegrationEventTestsOpencodeSessionStatusRepeated

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Opencode.Plugin
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeSessionEventCodec

let private promptText (arg: obj) =
    getPartsText (get (get arg "body") "parts")

let private userTextMessage sessionID text =
    box (
        createObj
            [ "info", box (createObj [ "role", box "user"; "sessionID", box sessionID ])
              "parts", box [| createObj [ "type", box "text"; "text", box text ] |] ]
    )

let sessionErrorWithoutFallbackTriggersNudgeSpec () =
    promise {
        let sessionID = "err-no-fallback-ws"

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

        let! workspaceDir = mkdtempAsync "err-no-fallback-"

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
            eventHook
            $ (mkEvent
                "session.error"
                (box
                    {| sessionID = sessionID
                       error =
                        box
                            {| name = "APIError"
                               message = "Quota exceeded"
                               isRetryable = false |} |}))
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()
        check "session.error without fallback does not trigger a nudge" (promptCalls.Count = 0)
        do! rmAsync workspaceDir
    }

let repeatedAssistantSpec () =
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
                            box (System.Func<obj, JS.Promise<unit>>(fun arg -> (promise { promptCalls.Add(arg) }))) ]
                  ) ]

        let! workspaceDir = mkdtempAsync "repeated-assistant-"

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = mkClient () |}
            )

        do!
            (get p "event")
            $ (box
                {| event =
                    box
                        {| ``type`` = "session.idle"
                           properties = box {| sessionID = "same-text-ws" |} |} |})
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()
        check "same text first assistant turn nudges" (promptCalls.Count = 1)

        messages <-
            Array.append
                messages
                [| box
                       {| info =
                           box
                               {| role = "assistant"
                                  agent = "manager"
                                  finish = "stop"
                                  time = box {| completed = 2 |} |}
                          parts =
                           [| box
                                  {| ``type`` = "text"
                                     text = "still working" |} |] |} |]

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
                           properties = box {| sessionID = "same-text-ws" |} |} |})
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()
        check "same text new assistant turn nudges again" (promptCalls.Count = 1)
        do! rmAsync workspaceDir
    }

let repeatedIdleBeforeHistoryPersistsNudgeSpec () =
    promise {
        let sessionID = "history-race-ws"

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
                                        promptCalls.Add(arg)

                                        messages <-
                                            Array.append messages [| userTextMessage sessionID (promptText arg) |]
                                    })
                            ) ]
                  ) ]

        let! workspaceDir = mkdtempAsync "repeated-idle-before-history-"

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = mkClient () |}
            )

        let eventHook = get p "event"

        let idle =
            box
                {| event =
                    box
                        {| ``type`` = "session.idle"
                           properties = box {| sessionID = sessionID |} |} |}

        do! eventHook $ idle |> unbox<JS.Promise<unit>>
        do! yieldMicrotask ()
        do! eventHook $ idle |> unbox<JS.Promise<unit>>
        do! yieldMicrotask ()
        check "repeated idle before nudge reaches history sends once" (promptCalls.Count = 1)
        do! rmAsync workspaceDir
    }
