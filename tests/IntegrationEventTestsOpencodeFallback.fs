module Wanxiangshu.Tests.IntegrationEventTestsOpencodeFallback

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Hosts.Opencode.Plugin
open Wanxiangshu.Runtime.Dyn

let fallbackRetryWithoutFrontmatterSpec () =
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
                                                                         text = "working" |} |] |} |] |}
                                    })
                            )
                            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ())) ]
                  ) ]

        let! workspaceDir = mkdtempAsync "fallback-retry-no-frontmatter-"

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = mkClient () |}
            )

        let eventHook = get p "event"
        let sid = "fallback-no-frontmatter-session"

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
                        {| ``type`` = "session.error"
                           properties =
                            box
                                {| info = box {| sessionID = sid |}
                                   error =
                                    box
                                        {| name = "APIError"
                                           message = "boom"
                                           isRetryable = true |} |} |} |})
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()
        equal "fallback without frontmatter retries once" 1 promptCalls.Count
        let call = promptCalls.[0]
        equal "retry path targets same session" sid (str (get call "path") "id")
        equal "retry body keeps current agent" "reviewer" (str (get call "body") "agent")
        equal "retry body keeps current model provider" "openai" (str (get (get call "body") "model") "providerID")
        equal "retry body keeps current model id" "gpt-5" (str (get (get call "body") "model") "modelID")
        do! rmAsync workspaceDir
    }

let sessionPostErrorSpec () =
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
                                                                         text = "working" |} |] |} |] |}
                                    })
                            )
                            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ())) ]
                  ) ]

        let! workspaceDir = mkdtempAsync "fallback-session-post-error-"

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = mkClient () |}
            )

        let sessionPostHook = get p "session.post"
        check "session.post hook should be registered" (not (isNullish sessionPostHook))

        let input =
            box
                {| sessionID = "post-err-session"
                   agentID = "reviewer"
                   outcome = "error"
                   error = "Quota limit reached" |}

        let output = createObj []
        do! sessionPostHook $ (input, output) |> unbox<JS.Promise<unit>>
        do! yieldMicrotask ()
        equal "session.post with error retries once" 1 promptCalls.Count
        do! rmAsync workspaceDir
    }

let sessionUserQueryPostErrorSpec () =
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
                                                                         text = "working" |} |] |} |] |}
                                    })
                            )
                            "abort", box (System.Func<obj, JS.Promise<unit>>(fun _ -> Promise.lift ())) ]
                  ) ]

        let! workspaceDir = mkdtempAsync "fallback-query-post-error-"

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = mkClient () |}
            )

        let sessionUserQueryPostHook = get p "session.userQuery.post"
        check "session.userQuery.post hook should be registered" (not (isNullish sessionUserQueryPostHook))

        let input =
            box
                {| sessionID = "query-err-session"
                   agentID = "reviewer"
                   step = 1
                   error = "Rate limit hit" |}

        let output = createObj []
        do! sessionUserQueryPostHook $ (input, output) |> unbox<JS.Promise<unit>>
        do! yieldMicrotask ()
        equal "session.userQuery.post with error retries once" 1 promptCalls.Count
        do! rmAsync workspaceDir
    }
