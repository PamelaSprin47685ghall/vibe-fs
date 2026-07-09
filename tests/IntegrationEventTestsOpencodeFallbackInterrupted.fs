module Wanxiangshu.Tests.IntegrationEventTestsOpencodeFallbackInterrupted

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Opencode.Plugin
open Wanxiangshu.Shell.Dyn

let sessionInterruptedEventSpec () =
    promise {
        let promptCalls = ResizeArray<obj>()

        let mkClient () =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "prompt",
                            box (System.Func<obj, JS.Promise<unit>>(fun arg -> promise { promptCalls.Add(arg) }))
                            "messages",
                            box (
                                System.Func<obj, JS.Promise<obj>>(fun _ ->
                                    promise {
                                        return
                                            box
                                                {| data =
                                                    [| box
                                                           {| info =
                                                               box
                                                                   {| role = "assistant"
                                                                      agent = "reviewer"
                                                                      model =
                                                                       box
                                                                           {| providerID = "openai"
                                                                              modelID = "gpt-5" |} |}
                                                              parts =
                                                               [| box
                                                                      {| ``type`` = "text"
                                                                         text = "<function=bash>exit 0</function>" |} |] |} |] |}
                                    })
                            )
                            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ())) ]
                  ) ]

        let! workspaceDir = mkdtempAsync "fallback-interrupted-"

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = mkClient () |}
            )

        let eventHook = get p "event"
        let sid = "interrupted-session"

        do!
            eventHook
            $ (box
                {| event =
                    box
                        {| ``type`` = "session.status"
                           properties =
                            box
                                {| info = box {| sessionID = sid |}
                                   status =
                                    box
                                        {| ``type`` = "busy"
                                           agent = "reviewer" |} |} |} |})
            |> unbox<JS.Promise<unit>>

        do!
            eventHook
            $ (box
                {| event =
                    box
                        {| ``type`` = "session.interrupted"
                           properties = box {| info = box {| sessionID = sid |} |} |} |})
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()
        equal "session.interrupted does not trigger continue prompt" 0 promptCalls.Count
        do! rmAsync workspaceDir
    }

let sessionInterruptedMessageIdleEventSpec () =
    promise {
        let promptCalls = ResizeArray<obj>()

        let mkClient () =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "prompt",
                            box (System.Func<obj, JS.Promise<unit>>(fun arg -> promise { promptCalls.Add(arg) }))
                            "messages",
                            box (
                                System.Func<obj, JS.Promise<obj>>(fun _ ->
                                    promise {
                                        let modelData =
                                            {| providerID = "openai"
                                               modelID = "gpt-5" |}

                                        let infoData =
                                            {| role = "assistant"
                                               agent = "reviewer"
                                               model = box modelData
                                               finish = "abort" |}

                                        let item = {| info = box infoData; parts = [||] |}
                                        return box {| data = [| box item |] |}
                                    })
                            )
                            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ())) ]
                  ) ]

        let! workspaceDir = mkdtempAsync "fallback-interrupted-msg-"

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = mkClient () |}
            )

        let eventHook = get p "event"
        let sid = "interrupted-msg-session"

        do!
            eventHook
            $ (box
                {| event =
                    box
                        {| ``type`` = "session.status"
                           properties =
                            box
                                {| info = box {| sessionID = sid |}
                                   status =
                                    box
                                        {| ``type`` = "busy"
                                           agent = "reviewer" |} |} |} |})
            |> unbox<JS.Promise<unit>>

        do!
            eventHook
            $ (box
                {| event =
                    box
                        {| ``type`` = "session.status"
                           properties =
                            box
                                {| info = box {| sessionID = sid |}
                                   status =
                                    box
                                        {| ``type`` = "idle"
                                           agent = "reviewer" |} |} |} |})
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()
        equal "interrupted message + idle event must not trigger continue prompt" 0 promptCalls.Count
        do! rmAsync workspaceDir
    }

let sessionInterruptedMessageWithContentIdleEventSpec () =
    promise {
        let promptCalls = ResizeArray<obj>()

        let mkClient () =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "prompt",
                            box (System.Func<obj, JS.Promise<unit>>(fun arg -> promise { promptCalls.Add(arg) }))
                            "messages",
                            box (
                                System.Func<obj, JS.Promise<obj>>(fun _ ->
                                    promise {
                                        let modelData =
                                            {| providerID = "openai"
                                               modelID = "gpt-5" |}

                                        let part1 =
                                            {| ``type`` = "text"
                                               text = "<function=bash>exit 0</function>" |}

                                        let infoData =
                                            {| role = "assistant"
                                               agent = "reviewer"
                                               model = box modelData
                                               finish = "abort" |}

                                        let item =
                                            {| info = box infoData
                                               parts = [| box part1 |] |}

                                        return box {| data = [| box item |] |}
                                    })
                            )
                            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ())) ]
                  ) ]

        let! workspaceDir = mkdtempAsync "fallback-interrupted-content-"

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = mkClient () |}
            )

        let eventHook = get p "event"
        let sid = "interrupted-content-session"

        do!
            eventHook
            $ (box
                {| event =
                    box
                        {| ``type`` = "session.status"
                           properties =
                            box
                                {| info = box {| sessionID = sid |}
                                   status =
                                    box
                                        {| ``type`` = "busy"
                                           agent = "reviewer" |} |} |} |})
            |> unbox<JS.Promise<unit>>

        do!
            eventHook
            $ (box
                {| event =
                    box
                        {| ``type`` = "session.status"
                           properties =
                            box
                                {| info = box {| sessionID = sid |}
                                   status =
                                    box
                                        {| ``type`` = "idle"
                                           agent = "reviewer" |} |} |} |})
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()
        equal "interrupted message with content + idle event must not trigger continue prompt" 0 promptCalls.Count
        do! rmAsync workspaceDir
    }
