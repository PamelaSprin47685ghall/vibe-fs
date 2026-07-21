module Wanxiangshu.Tests.IntegrationEventTestsOpencodeLoop

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Tests.EventLogTestSeed
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Runtime.LoopMessages
open Wanxiangshu.Runtime.PromptFragments
open Wanxiangshu.Runtime.PromptHeader
open Wanxiangshu.Hosts.Opencode.Plugin
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Messaging.OpencodeSessionEventCodec

let private loopAnchor task =
    frontMatterPrompt [ yamlField taskField task ] "With-Review Mode is active."

let private assistantMessage agent text completed =
    box
        {| info =
            box
                {| role = "assistant"
                   agent = agent
                   finish = "stop"
                   time = box {| completed = completed |} |}
           parts = [| box {| ``type`` = "text"; text = text |} |] |}

let opencodeLoopNudgeSpec () =
    promise {
        let sessionID = "opencode-loop-nudge-ws"
        let promptCalls = ResizeArray<obj>()
        let! workspaceDir = mkdtempAsync "opencode-loop-nudge-"

        let mutable messages: obj array =
            [| assistantMessage "manager" (loopAnchor "Ship the fix") 1 |]

        let mkClient (workspaceDir: string) =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "todo",
                            box (System.Func<unit, JS.Promise<obj>>(fun () -> promise { return box {| data = [||] |} }))
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
                                    })
                            ) ]
                  ) ]

        do! seedLoopActivated workspaceDir sessionID "Ship the fix"

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = mkClient workspaceDir |}
            )

        let eventHook = get p "event"

        do!
            eventHook
            $ (box
                {| event =
                    box
                        {| ``type`` = "session.idle"
                           properties = box {| sessionID = sessionID |} |} |})
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()

        let nudgeText =
            if promptCalls.Count = 0 then
                ""
            else
                getPartsText (get (get promptCalls.[0] "body") "parts")

        check "with-review idle emits loop nudge" (promptCalls.Count = 1 && nudgeText.Contains(loopNudgePromptProse))
        do! rmAsync workspaceDir
    }

let opencodeFreshChatMessageRearmsLoopNudgeSpec () =
    promise {
        let sessionID = "opencode-fresh-chat-ws"
        let promptCalls = ResizeArray<obj>()
        let! workspaceDir = mkdtempAsync "opencode-fresh-chat-"

        let mutable messages: obj array =
            [| assistantMessage "manager" (loopAnchor "Ship the fix") 1 |]

        let mkClient (workspaceDir: string) =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "todo",
                            box (System.Func<unit, JS.Promise<obj>>(fun () -> promise { return box {| data = [||] |} }))
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
                                    })
                            ) ]
                  ) ]

        do! seedLoopActivated workspaceDir sessionID "Ship the fix"

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = mkClient workspaceDir |}
            )

        let eventHook = get p "event"
        let chatHook = get p "chat.message"

        do!
            eventHook
            $ (box
                {| event =
                    box
                        {| ``type`` = "session.idle"
                           properties = box {| sessionID = sessionID |} |} |})
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()

        let textOf i =
            if promptCalls.Count <= i then
                ""
            else
                getPartsText (get (get promptCalls.[i] "body") "parts")

        check
            "first with-review idle emits loop nudge"
            (promptCalls.Count = 1 && (textOf 0).Contains(loopNudgePromptProse))

        do!
            chatHook
            $ (createObj [ "sessionID", box sessionID; "agent", box "manager" ],
               createObj
                   [ "parts",
                     box
                         [| box
                                {| ``type`` = "text"
                                   text = "still working on it" |} |] ])
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()
        messages <- Array.append messages [| assistantMessage "manager" "still working on it" 2 |]

        do!
            eventHook
            $ (box
                {| event =
                    box
                        {| ``type`` = "session.idle"
                           properties = box {| sessionID = sessionID |} |} |})
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()

        check
            "new assistant turn in history re-arms loop nudge on next idle"
            (promptCalls.Count = 2 && (textOf 1).Contains(loopNudgePromptProse))

        do! rmAsync workspaceDir
    }

let opencodeBrowserSubsessionHistoryDoesNotLoopNudgeSpec () =
    promise {
        let sessionID = "opencode-browser-child-ws"
        let promptCalls = ResizeArray<obj>()
        let! workspaceDir = mkdtempAsync "opencode-browser-child-"

        let mkClient (workspaceDir: string) =
            createObj
                [ "session",
                  box (
                      createObj
                          [ "todo",
                            box (System.Func<unit, JS.Promise<obj>>(fun () -> promise { return box {| data = [||] |} }))
                            "messages",
                            box (
                                System.Func<unit, JS.Promise<obj>>(fun () ->
                                    let reviewerPrompt =
                                        Wanxiangshu.Runtime.ReviewPrompts.Submission.reviewerPrompt
                                            "ship feature"
                                            ""
                                            []

                                    promise {
                                        return box {| data = [| assistantMessage "browser" reviewerPrompt 1 |] |}
                                    })
                            )
                            "prompt",
                            box (
                                System.Func<obj, JS.Promise<unit>>(fun arg ->
                                    promise {
                                        resolveNudgeReceiptFromPromptArg workspaceDir arg
                                        promptCalls.Add(arg)
                                    })
                            ) ]
                  ) ]

        let! p =
            plugin (
                box
                    {| directory = workspaceDir
                       client = mkClient workspaceDir |}
            )

        let eventHook = get p "event"

        do!
            eventHook
            $ (box
                {| event =
                    box
                        {| ``type`` = "session.idle"
                           properties = box {| sessionID = sessionID |} |} |})
            |> unbox<JS.Promise<unit>>

        do! yieldMicrotask ()
        check "reviewer-style child history must not trigger loop nudge" (promptCalls.Count = 0)
        do! rmAsync workspaceDir
    }
